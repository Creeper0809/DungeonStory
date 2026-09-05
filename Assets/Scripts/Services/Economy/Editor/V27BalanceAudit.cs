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

public static class V27BalanceAudit
{
    private enum MarketAuthorityState
    {
        Implemented,
        LegacyReconstructedBaseline,
        PreviouslyApprovedApplied,
        MissingProvenance,
        UnauthorizedDrift
    }

    public const string MarkdownPath = "docs/generated/V27_Balance_Before_After.md";
    public const string AuditPath = "Artifacts/QA/v27-balance-recalibration-audit.txt";
    public const string ManifestPath = "Artifacts/QA/v27-balance-artifact-manifest.json";
    public const string SourceInventoryPath =
        "Artifacts/QA/v27-balance-source-inventory.json";
    public const string ApprovalPath = "docs/game-design/v27-balance-critical-approvals.json";
    public const string BaselineRecordId = "architecture:v27-whitebox-ledger-pipeline";
    public const string EvidenceBaselineRecordId =
        "architecture:v27-whitebox-ledger-pipeline-evidence-gate-v1";
    public const string VerticalSliceBaselineRecordId =
        "balance:v27:logging-cooking-dismantle-vertical-slice";
    public const string SurvivalOutputBaselineRecordId =
        "architecture:v27-survival-cook-output-authority";
    public const string MarketBaselineRecordId =
        "balance:v27:item-market-asymmetric-price-authority";
    public const string LaborFacilityBaselineRecordId =
        "balance:v27:facility-bounded-wu-bom-redistribution-v2";
    public const string ResearchScheduleBaselineRecordId =
        "balance:v27:research-effective-output-period-preserving-v1";
    public const string DungeonExpansionBaselineRecordId =
        "balance:v27:existing-mining-research-dungeon-expansion-authority-v4";
    public const string DungeonExpansionWidthBaselineRecordId =
        "balance:v27:storage-bounded-dungeon-expansion-widths-v3";
    public const string LaborAuthorityBaselineRecordId =
        "balance:v27:actual-effective-labor-authority-downstream-v1";
    public const string LaborMatrixBaselineRecordId =
        "balance:v27:population-technology-survival-emergency-matrix-v1";
    public const string EquipmentReadinessBaselineRecordId =
        "balance:v27:equipment-readiness-quality-schedule-v1";
    public const string CombatOutcomeBaselineRecordId =
        "balance:v27:combat-after-equipment-quality-minimal-recalibration-v1";
    public const string DailyRoutineEvidenceBaselineRecordId =
        "balance:v27:daily-routine-post-recalibration-wu-evidence-v1";
    public const string ServiceContinuityBaselineRecordId =
        "balance:v27:service-continuity-nplusone";
    public const string PrimitiveFallbackBaselineRecordId =
        "balance:v27:primitive-fallback-capital-relief";
    public const string SharedAccessBaselineRecordId =
        "balance:v27:shared-access-spatial-union";
    public const string FloorClutterBaselineRecordId =
        "balance:v27:floor-clutter-runtime-capacity";
    public const string OverflowContainmentBaselineRecordId =
        "balance:v27:storage-overflow-containment";
    public const string CounterfactualRngBaselineRecordId =
        "balance:v27:counterfactual-rng-isolation";
    public const string PairedRunBaselineRecordId =
        "balance:v27:paired-run-window-attribution";
    public const string PopulationCapacityBaselineRecordId =
        "balance:v27:population-stage-capacity";
    public const string SixAdultClosedLoopBaselineRecordId =
        "balance:v27:six-adult-food-water-closed-loop";
    public const string IntegratedCapacityValidationBaselineRecordId =
        "balance:v27:service-spatial-clutter-rng-validation-v1";
    public const string OutputContainmentBaselineRecordId =
        "balance:v27:resource-output-containment-saturation-v1";
    public const string MultiOutputEconomicAllocationBaselineRecordId =
        "balance:v27:multi-output-economic-allocation-v1";
    internal const string ConstructionRecalibrationCandidateWuMetric =
        "construction-recalibration-candidate-wu";
    internal const string ConstructionRecalibrationCandidateMaterialMetricPrefix =
        "construction-recalibration-candidate-material:";
    internal const string MarketRecalibrationCandidateMetricPrefix =
        "market-recalibration-candidate:";
    internal const string MarketDerivedRecalibrationCandidateMetricPrefix =
        "market-derived-recalibration-candidate:";
    private const string GeneratorVersion = "v27.13.3";
    private const decimal LaborScale = 2.25m;

    [MenuItem("DungeonStory/V27/Generate Audit-Only Whole-Game Ledger")]
    public static void GenerateAuditOnly()
    {
        V27BalanceAuditOutput output = Generate(BalanceLedgerExecutionMode.AuditOnly);
        Debug.Log(
            $"V27 audit-only ledger generated: rows={output.Ledger.Count}, "
            + $"critical={output.CriticalCount}, scc={output.SccCount}, "
            + $"integrityFailures={output.IntegrityFailures.Count}.");
        if (output.IntegrityFailures.Count > 0)
        {
            throw new InvalidOperationException(
                "V27 audit integrity failed:\n" + string.Join("\n", output.IntegrityFailures));
        }
    }

    public static V27BalanceAuditOutput Generate(BalanceLedgerExecutionMode mode)
    {
        return Generate(mode, allowApprovalRefresh: false);
    }

    internal static V27BalanceAuditOutput GenerateForApprovalRefresh()
    {
        return Generate(BalanceLedgerExecutionMode.AuditOnly, allowApprovalRefresh: true);
    }

    [MenuItem("DungeonStory/V27/Generate Dependency Recalibration Review")]
    public static void GenerateDependencyRecalibrationReview()
    {
        V27BalanceAuditOutput output = GenerateForApprovalRefresh();
        if (output.IntegrityFailures.Count > 0)
        {
            throw new InvalidOperationException(
                "V27 dependency recalibration integrity failed:\n"
                + string.Join("\n", output.IntegrityFailures));
        }
        Debug.Log(
            $"V27 dependency recalibration review generated: rows={output.Ledger.Count}, "
            + $"critical={output.CriticalCount}, scc={output.SccCount}. "
            + "Criticals remain review gates; no asset or approval was changed.");
    }

    private static V27BalanceAuditOutput Generate(
        BalanceLedgerExecutionMode mode,
        bool allowApprovalRefresh)
    {
        if (mode != BalanceLedgerExecutionMode.AuditOnly
            && mode != BalanceLedgerExecutionMode.RegenerateArtifacts)
        {
            throw new InvalidOperationException(
                "The audit generator cannot mutate assets. Use the approved asset applier.");
        }

        V27EditorContentSource source = V27EditorContentSource.Load();
        ResourceMaterialEconomicProfileCatalog materialProfiles = new(source);
        V23BalanceWorkCalculator work = new(materialProfiles);
        V23MaterialSalvageCalculator salvage = new(materialProfiles);
        ProductionRecipeSO[] recipes = source.GetAll<ProductionRecipeSO>()
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        CropDefinitionSO[] crops = source.GetAll<CropDefinitionSO>()
            .OrderBy(value => value.CropId, StringComparer.Ordinal)
            .ToArray();
        ItemDefinitionSO[] items = source.GetAll<ItemDefinitionSO>()
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        CombatEquipmentDefinitionSO[] equipment = source
            .GetAll<CombatEquipmentDefinitionSO>()
            .OrderBy(value => value.EquipmentId, StringComparer.Ordinal)
            .ToArray();
        CraftMaterialDefinitionSO[] materials = source
            .GetAll<CraftMaterialDefinitionSO>()
            .OrderBy(value => value.MaterialId, StringComparer.Ordinal)
            .ToArray();
        EmbeddedWorkValueSnapshot before = new V23EmbeddedWorkValueCalculator(
            recipes,
            items,
            equipment,
            materials,
            work).Calculate();
        IReadOnlyDictionary<string, string> historicalBeforeValues =
            V27BalanceAssetApplication.CaptureHistoricalBeforeValues();
        IReadOnlyDictionary<string, string> previouslyApprovedAfterValues =
            V27BalanceAssetApplication.CapturePreviouslyApprovedAfterValues();
        V27EmbeddedWorkValueSnapshot after = new V27EmbeddedWorkValueCalculator(
            recipes,
            crops,
            items,
            equipment,
            materials,
            before,
            work,
            materialProfiles,
            LaborScale,
            historicalBeforeValues).Calculate();
        IReadOnlyDictionary<string, long> routeComparableBeforeItemValues =
            BuildRouteComparableBeforeItemValues(
                crops,
                items,
                before,
                historicalBeforeValues);

        List<string> integrityFailures = new List<string>();
        if (recipes.Length != 355)
            integrityFailures.Add($"Expected 355 recipes, found {recipes.Length}.");
        if (before.UnresolvedItemIds.Count > 0)
            integrityFailures.Add("V23 unresolved items: " + string.Join(",", before.UnresolvedItemIds));
        if (before.NonConvergentRecipeIds.Count > 0)
            integrityFailures.Add(
                "V23 non-convergent recipes: " + string.Join(",", before.NonConvergentRecipeIds));
        if (after.UnresolvedItemIds.Count > 0)
            integrityFailures.Add("V27 unresolved items: " + string.Join(",", after.UnresolvedItemIds));
        if (after.NonConvergentRecipeIds.Count > 0)
            integrityFailures.Add(
                "V27 non-convergent recipes: " + string.Join(",", after.NonConvergentRecipeIds));

        Dictionary<string, int> downstream = BuildDownstreamCounts(recipes, crops);
        HashSet<string> itemValueSemanticRootIds = recipes
            .Select(value => RawStableId(value, "recipeId"))
            .Concat(crops.Select(value => RawStableId(value, "cropId")))
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> sourceDigests = new Dictionary<string, string>(
            StringComparer.Ordinal);
        CapturePipelineSourceDigests(sourceDigests);
        BalanceCaptureFactory capture = new BalanceCaptureFactory();
        List<BalanceAnomalyNode> anomalies = new List<BalanceAnomalyNode>();
        CaptureLaborTargets(capture, anomalies, sourceDigests);
        CaptureIntegratedCapacityMetrics(capture, sourceDigests);
        CaptureResearchScheduleTargets(
            source.GetAll<ResearchProjectSO>(),
            capture,
            sourceDigests,
            historicalBeforeValues);
        CaptureSerializedAuthority(source, capture, sourceDigests);
        CaptureItemValues(
            items,
            before,
            after,
            materialProfiles,
            itemValueSemanticRootIds,
            downstream,
            capture,
            anomalies,
            sourceDigests,
            integrityFailures,
            historicalBeforeValues,
            previouslyApprovedAfterValues,
            allowApprovalRefresh);
        CaptureItemMarketConsumers(
            source,
            items,
            before,
            after,
            capture,
            anomalies,
            sourceDigests,
            integrityFailures,
            historicalBeforeValues,
            previouslyApprovedAfterValues,
            allowApprovalRefresh);
        CaptureCropValues(
            crops,
            items,
            before,
            after,
            capture,
            anomalies,
            sourceDigests,
            historicalBeforeValues);
        CaptureRecipeValues(
            recipes,
            before,
            after,
            routeComparableBeforeItemValues,
            capture,
            anomalies,
            sourceDigests,
            historicalBeforeValues);
        IReadOnlyDictionary<string, V27ConstructionRedistributionResult>
            constructionCandidates = CaptureBuildingCandidates(
            source.GetAll<BuildingSO>(),
            before,
            after,
            work,
            capture,
            anomalies,
            sourceDigests,
            historicalBeforeValues,
            previouslyApprovedAfterValues);
        CaptureCombatEncounterValues(
            source.GetAll<OffenseEncounterSO>(),
            capture,
            anomalies,
            sourceDigests,
            historicalBeforeValues);
        CaptureDismantleCycles(
            source.GetAll<BuildingSO>(),
            before,
            after,
            salvage,
            capture,
            anomalies,
            sourceDigests,
            constructionCandidates);

        BalanceTransform[] transforms = recipes
            .Where(recipe => recipe.Inputs.Count > 0
                && after.Recipes.ContainsKey(recipe.RecipeId))
            .Select(recipe =>
            {
                V27RecipeValueBreakdown value = after.Recipes[recipe.RecipeId];
                return BalanceTransform.Capture(
                    recipe.RecipeId,
                    recipe.Inputs.Select(input => input.ItemId),
                    recipe.Outputs.Where(output => output.Probability > 0f)
                        .Select(output => output.ItemId),
                    value.TotalDebit.MilliEwu,
                    value.TotalOutputCredit.MilliEwu);
            })
            .Concat(crops
                .Where(crop => after.Crops.ContainsKey(crop.CropId))
                .Select(crop =>
                {
                    V27CropValueBreakdown value = after.Crops[crop.CropId];
                    return BalanceTransform.Capture(
                        crop.CropId,
                        value.CleanWaterUnits > 0
                            ? new[] { "resource:clean-water" }
                            : Array.Empty<string>(),
                        new[] { crop.HarvestItemId },
                        value.TotalDebit.MilliEwu,
                        value.TotalOutputCredit.MilliEwu);
                }))
            .Concat(BuildDismantleTransforms(
                source.GetAll<BuildingSO>(),
                after,
                salvage,
                constructionCandidates))
            .ToArray();
        BalanceSccAuditResult scc = BalanceSccAuditor.Audit(transforms);
        foreach (string violation in scc.ViolatingTransformIds)
        {
            anomalies.Add(BalanceAnomalyNode.Capture(
                violation,
                "scc-margin",
                BalanceAnomalySeverity.Critical,
                BalanceAnomalyDisposition.LocalCritical,
                "non-negative-transform-margin",
                Array.Empty<string>()));
            integrityFailures.Add($"SCC transform has non-negative margin: {violation}.");
        }

        if (!File.Exists(ProjectAbsolutePath(ApprovalPath)))
            integrityFailures.Add("Critical approval authority is missing.");
        AddPipelineReadinessAnomalies(anomalies);

        FrozenBalanceLedger ledger = capture.Freeze();
        string[] approvedKeys = allowApprovalRefresh
            ? V27BalanceAssetApplication.CaptureMatchingApprovalKeysForRefresh(ledger)
            : V27BalanceAssetApplication.CaptureValidApprovalKeys(ledger);
        PromoteDependencyRoots(ledger, anomalies, approvedKeys);
        anomalies = ApplyApprovalDispositions(ledger, anomalies, approvedKeys);
        string assetPatchDigest = allowApprovalRefresh
            ? string.Empty
            : V27BalanceAssetApplication.CaptureApprovedPatchDigest(ledger);
        ledger = BalanceLedgerReviewFactory.ApplyApprovedKeys(ledger, approvedKeys);
        BalanceAnomalyNode[] orderedAnomalies = anomalies
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ThenBy(value => value.Metric, StringComparer.Ordinal)
            .ThenBy(value => value.ReasonCode, StringComparer.Ordinal)
            .ToArray();

        bool csvChanged = V27BalanceArtifactWriter.WriteCsvIfDifferent(
            V27BalanceCsvSerializer.ArtifactPath,
            ledger);
        bool markdownChanged = V27BalanceArtifactWriter.WriteIfDifferent(MarkdownPath, stream =>
            WriteMarkdown(stream, ledger, orderedAnomalies, scc, integrityFailures));
        bool anomalyChanged = V27BalanceArtifactWriter.WriteIfDifferent(
            V27BalanceJsonSerializer.AnomalyArtifactPath,
            stream => V27BalanceJsonSerializer.WriteAnomalyGraph(stream, orderedAnomalies));
        bool auditChanged = V27BalanceArtifactWriter.WriteIfDifferent(AuditPath, stream =>
            WriteAudit(stream, ledger, orderedAnomalies, scc, integrityFailures));

        string aggregateSourceDigest = HashCanonicalPairs(sourceDigests);
        bool sourceInventoryChanged = V27BalanceArtifactWriter.WriteIfDifferent(SourceInventoryPath, stream =>
            WriteSourceInventory(stream, sourceDigests));
        string csvHash = V27BalanceArtifactWriter.ComputeSha256(
            V27BalanceCsvSerializer.ArtifactPath);
        string markdownHash = V27BalanceArtifactWriter.ComputeSha256(MarkdownPath);
        string auditHash = V27BalanceArtifactWriter.ComputeSha256(AuditPath);
        string anomalyHash = V27BalanceArtifactWriter.ComputeSha256(
            V27BalanceJsonSerializer.AnomalyArtifactPath);
        string sourceInventoryHash = V27BalanceArtifactWriter.ComputeSha256(
            SourceInventoryPath);
        string approvalHash = V27BalanceArtifactWriter.ComputeSha256(ApprovalPath);
        BalanceAuthoritySnapshot authoritySnapshot = BalanceAuthoritySnapshot.Capture(
            ledger,
            aggregateSourceDigest,
            sourceDigests.Count);
        BalanceArtifactManifest artifactManifest = BalanceArtifactManifest.Capture(
            "v27.1",
            GeneratorVersion,
            authoritySnapshot,
            orderedAnomalies.Count(value => value.EmitsCiAnnotation),
            orderedAnomalies.Count(value =>
                value.Severity == BalanceAnomalySeverity.Critical
                && !value.EmitsCiAnnotation),
            approvedKeys.Length,
            scc.Components.Count,
            integrityFailures.Count,
            CaptureBaselineRecordIds());
        bool manifestChanged = V27BalanceArtifactWriter.WriteIfDifferent(ManifestPath, stream =>
            WriteManifest(
                stream,
                artifactManifest,
                csvHash,
                markdownHash,
                auditHash,
                anomalyHash,
                sourceInventoryHash,
                approvalHash,
                assetPatchDigest));
        V27BalanceAuditWriteResult writeResult = V27BalanceAuditWriteResult.Capture(
            new[]
            {
                V27BalanceAuditWriteObservation.Capture(
                    V27BalanceCsvSerializer.ArtifactPath, csvChanged),
                V27BalanceAuditWriteObservation.Capture(
                    MarkdownPath, markdownChanged),
                V27BalanceAuditWriteObservation.Capture(
                    V27BalanceJsonSerializer.AnomalyArtifactPath, anomalyChanged),
                V27BalanceAuditWriteObservation.Capture(
                    AuditPath, auditChanged),
                V27BalanceAuditWriteObservation.Capture(
                    SourceInventoryPath, sourceInventoryChanged),
                V27BalanceAuditWriteObservation.Capture(
                    ManifestPath, manifestChanged)
            });
        AssetDatabase.Refresh();
        return new V27BalanceAuditOutput(
            authoritySnapshot,
            artifactManifest,
            assetPatchDigest,
            Array.AsReadOnly(orderedAnomalies),
            Array.AsReadOnly(integrityFailures.ToArray()),
            writeResult);
    }

    public static void RefreshManifestEvidenceHashes(
        V27BalanceAuditOutput output)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        string csvHash = V27BalanceArtifactWriter.ComputeSha256(
            V27BalanceCsvSerializer.ArtifactPath);
        string markdownHash = V27BalanceArtifactWriter.ComputeSha256(MarkdownPath);
        string auditHash = V27BalanceArtifactWriter.ComputeSha256(AuditPath);
        string anomalyHash = V27BalanceArtifactWriter.ComputeSha256(
            V27BalanceJsonSerializer.AnomalyArtifactPath);
        string sourceInventoryHash = V27BalanceArtifactWriter.ComputeSha256(
            SourceInventoryPath);
        string approvalHash = V27BalanceArtifactWriter.ComputeSha256(ApprovalPath);
        V27BalanceArtifactWriter.WriteIfDifferent(ManifestPath, stream =>
            WriteManifest(
                stream,
                output.ArtifactManifest,
                csvHash,
                markdownHash,
                auditHash,
                anomalyHash,
                sourceInventoryHash,
                approvalHash,
                output.AssetPatchDigest));
    }

    private static void CaptureCombatEncounterValues(
        IEnumerable<OffenseEncounterSO> definitions,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        Dictionary<string, OffenseEncounterSO> byId = definitions
            .Where(value => value != null)
            .ToDictionary(value => value.encounterId, StringComparer.Ordinal);
        foreach (CombatEncounterCalibration calibration in
                 CombatBalanceCheckpointAuthority.AllEncounters)
        {
            if (!byId.TryGetValue(calibration.EncounterId, out OffenseEncounterSO definition))
            {
                throw new InvalidOperationException(
                    $"Missing calibrated encounter authority: {calibration.EncounterId}.");
            }
            string path = AssetDatabase.GetAssetPath(definition);
            CaptureCombatEncounterMetric(
                definition, path, "enemy-health-multiplier", "enemyHealthMultiplier",
                definition.enemyHealthMultiplier, calibration.EnemyHealthMultiplier,
                capture, anomalies, sourceDigests, historicalBeforeValues);
            CaptureCombatEncounterMetric(
                definition, path, "enemy-damage-multiplier", "enemyDamageMultiplier",
                definition.enemyDamageMultiplier, calibration.EnemyDamageMultiplier,
                capture, anomalies, sourceDigests, historicalBeforeValues);
            CaptureCombatEncounterMetric(
                definition, path, "enemy-accuracy-multiplier", "enemyAccuracyMultiplier",
                definition.enemyAccuracyMultiplier, calibration.EnemyAccuracyMultiplier,
                capture, anomalies, sourceDigests, historicalBeforeValues);
            CaptureCombatEncounterMetric(
                definition, path, "objective-health-multiplier", "objectiveHealthMultiplier",
                definition.objectiveHealthMultiplier, calibration.ObjectiveHealthMultiplier,
                capture, anomalies, sourceDigests, historicalBeforeValues);
            CaptureCombatEncounterMetric(
                definition, path, "objective-control-resistance-multiplier",
                "objectiveControlResistanceMultiplier",
                definition.objectiveControlResistanceMultiplier,
                calibration.ObjectiveControlResistanceMultiplier,
                capture, anomalies, sourceDigests, historicalBeforeValues);
            CaptureCombatEncounterMetric(
                definition, path, "additional-enemy-count", "additionalEnemyCount",
                definition.additionalEnemyCount, calibration.AdditionalEnemyCount,
                capture, anomalies, sourceDigests, historicalBeforeValues);
            CaptureCombatEncounterMetric(
                definition, path, "objective-round-limit", "objectiveRoundLimit",
                definition.objectiveRoundLimit,
                definition.objective == OffenseEncounterObjective.DefeatAll
                    ? 0
                    : calibration.ObjectiveRoundLimit,
                capture, anomalies, sourceDigests, historicalBeforeValues);
        }
    }

    private static void CaptureCombatEncounterMetric(
        OffenseEncounterSO definition,
        string path,
        string metric,
        string propertyPath,
        float currentValue,
        float targetValue,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        CaptureCombatEncounterMetric(
            definition,
            path,
            metric,
            propertyPath,
            BalanceCanonicalText.DecimalFromFiniteFloat(
                currentValue,
                definition.encounterId + ":" + propertyPath),
            BalanceCanonicalText.DecimalFromFiniteFloat(
                targetValue,
                definition.encounterId + ":target:" + propertyPath),
            capture,
            anomalies,
            sourceDigests,
            historicalBeforeValues);
    }

    private static void CaptureCombatEncounterMetric(
        OffenseEncounterSO definition,
        string path,
        string metric,
        string propertyPath,
        int currentValue,
        int targetValue,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests,
        IReadOnlyDictionary<string, string> historicalBeforeValues) =>
        CaptureCombatEncounterMetric(
            definition,
            path,
            metric,
            propertyPath,
            (decimal)currentValue,
            (decimal)targetValue,
            capture,
            anomalies,
            sourceDigests,
            historicalBeforeValues);

    private static void CaptureCombatEncounterMetric(
        OffenseEncounterSO definition,
        string path,
        string metric,
        string propertyPath,
        decimal current,
        decimal target,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        string stableId = definition.encounterId;
        decimal before = ResolveHistoricalAuthoredBefore(
            stableId,
            metric,
            current,
            historicalBeforeValues);
        if (current != before && current != target)
        {
            throw new InvalidOperationException(
                $"Combat encounter value drifted outside its V27 Before/After contract: "
                + $"{stableId}:{propertyPath}; current={Token(current)}, "
                + $"before={Token(before)}, after={Token(target)}.");
        }
        string afterToken = Token(target);
        string dependencyFingerprint = HashText(
            definition.objective + "|" + definition.elite + "|" + definition.boss
            + "|" + definition.objectiveTargetId + "|"
            + string.Join(",", definition.enemies.Select(value => value.enemyArchetypeId)));
        string sourceDigest = GetEncounterApprovalSourceDigest(path);
        decimal percent = PercentDelta(before, target);
        BalanceAnomalySeverity severity = BalanceAnomalyDetector.ClassifyPercentDelta(
            Math.Abs(percent));
        const string reasonCode = "v27-combat-1000-seed-outcome-calibration";
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = "combat",
            DefinitionKind = "offense-encounter",
            StableId = stableId,
            Metric = metric,
            Unit = propertyPath.EndsWith("Count", StringComparison.Ordinal)
                || propertyPath.EndsWith("Limit", StringComparison.Ordinal)
                ? "count"
                : "multiplier",
            Before = Token(before),
            After = afterToken,
            AuthoredRoundedValue = afterToken,
            PercentDelta = Token(percent),
            ExactFormula = "accepted production-tactics 1000-seed checkpoint",
            BeforeBom = "N/A",
            AfterBom = "N/A",
            BeforeDirectWu = "N/A",
            AfterDirectWu = "N/A",
            BeforeBomEwu = "N/A",
            AfterBomEwu = "N/A",
            BeforeLaborDensity = "N/A",
            AfterLaborDensity = "N/A",
            UpstreamOnlyAfter = Token(before),
            InheritedDelta = "0",
            RawLocalDelta = Token(target - before),
            LocalQuantizationBoundaryCount = 0,
            DownstreamConsumerCount = "battle-runtime",
            DependencyIds = definition.enemies
                .Select(value => value.enemyArchetypeId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            RootCauseIds = Array.Empty<string>(),
            AnomalyDisposition = severity == BalanceAnomalySeverity.Critical
                ? "local-critical"
                : severity == BalanceAnomalySeverity.Warning ? "warning" : "none",
            ReasonCode = reasonCode,
            ReasonDetail = "Exact value accepted only after production EnemyTacticalDecisionService "
                + "and mixed-party deterministic checkpoint passed 1,000 seeds.",
            SourceAuthority = path,
            SourcePropertyPath = propertyPath,
            ExecutionRoute = "OffenseEncounterSO->EnemyEncounterFactory->OffenseBattleModel",
            SaveAuthority = "OffenseEncounterSO",
            VerificationEvidence = "Artifacts/QA/combat-balance-final/encounter-"
                + stableId.Substring("encounter:".Length) + ".txt",
            ReviewStatus = before == target ? "unchanged" : "pending",
            ApprovalKey = before != target
                ? BuildApprovalKey(
                    stableId,
                    metric,
                    afterToken,
                    dependencyFingerprint,
                    sourceDigest,
                    reasonCode,
                    CombatOutcomeBaselineRecordId)
                : string.Empty,
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = HashText(stableId + "|" + metric + "|" + Token(before)),
            SourceDigest = sourceDigest,
            SemanticHash = HashText(stableId + "|" + metric + "|" + afterToken),
            AssetApplied = current == target ? "true" : "false",
            BalanceBaselineRecordId = CombatOutcomeBaselineRecordId
        });
        if (severity != BalanceAnomalySeverity.None && before != target)
        {
            anomalies.Add(BalanceAnomalyNode.Capture(
                stableId,
                metric,
                severity,
                severity == BalanceAnomalySeverity.Critical
                    ? BalanceAnomalyDisposition.LocalCritical
                    : BalanceAnomalyDisposition.None,
                reasonCode,
                Array.Empty<string>()));
        }
    }

    private static void CaptureLaborTargets(
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests)
    {
        const string sourcePath =
            "Assets/Scripts/Models/Work/SettlementLaborAuthority.cs";
        string sourceDigest = GetSourceDigest(sourcePath, sourceDigests);
        decimal[] historicalActual = { 20m, 20.8m, 22m, 23.4m, 24.6m, 25.83m };
        decimal[] historicalEffective = { 20m, 21.84m, 25.08m, 29.884m, 33.948m, 40m };
        IReadOnlyList<TechnologyWuCheckpoint> checkpoints =
            SettlementLaborBalanceRules.TechnologyCheckpoints;
        for (int stage = 0; stage < checkpoints.Count; stage++)
        {
            CaptureLaborMetric(
                capture, anomalies, sourcePath, sourceDigest, stage,
                "actual-wu-per-adult-day",
                historicalActual[stage],
                Convert.ToDecimal(checkpoints[stage].ActualLaborWu, CultureInfo.InvariantCulture));
            CaptureLaborMetric(
                capture, anomalies, sourcePath, sourceDigest, stage,
                "effective-wu-per-adult-day",
                historicalEffective[stage],
                Convert.ToDecimal(checkpoints[stage].OutputEquivalentWu, CultureInfo.InvariantCulture));
        }
    }

    private static void CaptureLaborMetric(
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        string sourcePath,
        string sourceDigest,
        int stage,
        string metric,
        decimal before,
        decimal after)
    {
        string stableId = "technology-stage:" + stage.ToString(CultureInfo.InvariantCulture);
        decimal percent = PercentDelta(before, after);
        BalanceAnomalySeverity severity = BalanceAnomalyDetector.ClassifyPercentDelta(
            Math.Abs(percent));
        string fingerprint = HashText(metric + "|" + stage.ToString(CultureInfo.InvariantCulture));
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = "labor",
            DefinitionKind = "technology-stage",
            StableId = stableId,
            Metric = metric,
            Unit = "WU/adult-day",
            Before = Token(before),
            After = Token(after),
            AuthoredRoundedValue = Token(after),
            PercentDelta = Token(percent),
            ExactFormula = stage == 0
                ? "measured stable-AI target"
                : "V26 stage multiplier mapped to V27 authored target",
            BeforeBom = "N/A",
            AfterBom = "N/A",
            BeforeDirectWu = Token(before),
            AfterDirectWu = Token(after),
            BeforeBomEwu = "N/A",
            AfterBomEwu = "N/A",
            BeforeLaborDensity = "N/A",
            AfterLaborDensity = "N/A",
            UpstreamOnlyAfter = Token(before),
            InheritedDelta = "0",
            RawLocalDelta = Token(after - before),
            LocalQuantizationBoundaryCount = 0,
            DownstreamConsumerCount = "all-schedules",
            DependencyIds = Array.Empty<string>(),
            RootCauseIds = Array.Empty<string>(),
            AnomalyDisposition = severity == BalanceAnomalySeverity.Critical
                ? "root-critical"
                : severity == BalanceAnomalySeverity.Warning ? "warning" : "none",
            ReasonCode = "stable-ai-five-day-measurement",
            ReasonDetail = "Actual seeds 44.418/48.882/53.126; authored actual=50, effective=45.",
            SourceAuthority = sourcePath,
            SourcePropertyPath = "V27 labor authority",
            ExecutionRoute = "DailyRoutineWuPlayModeVerifier",
            SaveAuthority = "authored balance constants",
            VerificationEvidence = "seeds:157181|157182|157183",
            ReviewStatus = severity == BalanceAnomalySeverity.Critical ? "pending" : "review",
            ApprovalKey = string.Empty,
            DependencyFingerprint = fingerprint,
            LocalFingerprint = fingerprint,
            SourceDigest = sourceDigest,
            SemanticHash = HashText(stableId + "|" + metric + "|" + Token(after)),
            AssetApplied = "false",
            BalanceBaselineRecordId = BaselineRecordId
        });
        if (severity != BalanceAnomalySeverity.None)
        {
            anomalies.Add(BalanceAnomalyNode.Capture(
                stableId,
                metric,
                severity,
                severity == BalanceAnomalySeverity.Critical
                    ? BalanceAnomalyDisposition.RootCritical
                    : BalanceAnomalyDisposition.None,
                "stable-ai-five-day-measurement",
                Array.Empty<string>()));
        }
    }

    private static void CaptureResearchScheduleTargets(
        IEnumerable<ResearchProjectSO> definitions,
        BalanceCaptureFactory capture,
        IDictionary<string, string> sourceDigests,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        ResearchProjectSO[] projects = definitions
            .Where(value => value != null)
            .OrderBy(value => value.ProjectId.Value, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, int> downstream = projects
            .SelectMany(project => project.PrerequisiteIds.Select(prerequisite =>
                prerequisite.Value))
            .GroupBy(value => value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        const string metric = "authored-research-required-wu";
        const string reasonCode = "v27-research-duration-preserving-effective-authority";
        foreach (ResearchProjectSO project in projects)
        {
            string stableId = project.ProjectId.Value;
            string baselineRecordId = DungeonSpaceExpansionCatalog.TryGet(
                stableId,
                out _)
                    ? DungeonExpansionWidthBaselineRecordId
                    : ResearchScheduleBaselineRecordId;
            string path = BalanceCanonicalText.ProjectRelativePath(
                AssetDatabase.GetAssetPath(project));
            decimal current = BalanceCanonicalText.DecimalFromFiniteFloat(
                project.RequiredWork,
                stableId + ":requiredWork");
            decimal before = ResolveHistoricalAuthoredBefore(
                stableId,
                metric,
                current,
                historicalBeforeValues);
            decimal after = decimal.Ceiling(
                before
                * Convert.ToDecimal(
                    SettlementLaborAuthority.EffectiveOutputWuPerAdultDay,
                    CultureInfo.InvariantCulture)
                / Convert.ToDecimal(
                    SettlementLaborAuthority.HistoricalTheoreticalCapacityWuPerAdultDay,
                    CultureInfo.InvariantCulture));
            if (current != before && current != after)
            {
                throw new InvalidOperationException(
                    $"Research work drifted outside its V27 Before/After contract: "
                    + $"{stableId}; current={Token(current)}, before={Token(before)}, "
                    + $"after={Token(after)}.");
            }

            string[] dependencies = project.PrerequisiteIds
                .Select(value => value.Value)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string dependencyFingerprint = HashText(
                stableId + "|" + project.Field + "|" + project.MaximumResearchers + "|"
                + project.BlueprintRule + "|" + project.BlueprintId + "|"
                + string.Join("|", dependencies));
            string sourceDigest = HashText(
                GetApprovalSourceDigest(path, "requiredWork") + "|"
                + GetSourceDigest(
                    "Assets/Scripts/Services/Research/Editor/ResearchProjectAssetBuilder.cs",
                    sourceDigests) + "|"
                + GetSourceDigest(
                    "Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs",
                    sourceDigests) + "|"
                + GetSourceDigest(
                    "Assets/Scripts/Models/Work/SettlementLaborAuthority.cs",
                    sourceDigests));
            string afterToken = Token(after);
            int consumerCount = downstream.TryGetValue(stableId, out int count) ? count : 0;
            capture.Capture(new BalanceMetricCaptureRequest
            {
                Domain = "research",
                DefinitionKind = "research-project",
                StableId = stableId,
                Metric = metric,
                Unit = "WU",
                Before = Token(before),
                After = afterToken,
                AuthoredRoundedValue = afterToken,
                PercentDelta = Token(PercentDelta(before, after)),
                ExactFormula = "ceil(V26 requiredWork*45 effective WU/99 historical WU)",
                BeforeBom = "N/A",
                AfterBom = "N/A",
                BeforeDirectWu = Token(before),
                AfterDirectWu = afterToken,
                BeforeBomEwu = "N/A",
                AfterBomEwu = "N/A",
                BeforeLaborDensity = "N/A",
                AfterLaborDensity = "N/A",
                UpstreamOnlyAfter = Token(before),
                InheritedDelta = "0",
                RawLocalDelta = Token(after - before),
                LocalQuantizationBoundaryCount = 1,
                DownstreamConsumerCount = consumerCount.ToString(CultureInfo.InvariantCulture),
                DependencyIds = dependencies,
                RootCauseIds = Array.Empty<string>(),
                AnomalyDisposition = "review",
                ReasonCode = reasonCode,
                ReasonDetail = "The authored research tree used the historical 99 WU/day "
                    + "pacing divisor. Re-authoring with ceil(Before*45/99) preserves the "
                    + "existing calendar bands under the current effective-output authority.",
                SourceAuthority = path,
                SourcePropertyPath = "requiredWork",
                ExecutionRoute = "ResearchProjectSO->ResearchWorkExecutionHandler->AIWork",
                SaveAuthority = "ResearchProjectSO.requiredWork; active progress saves completedWork",
                VerificationEvidence = string.Equals(
                    baselineRecordId,
                    DungeonExpansionBaselineRecordId,
                    StringComparison.Ordinal)
                        ? "DungeonSpaceExpansionDebugScenarios + "
                          + "DungeonSpaceExpansionPlayModeVerifier + "
                          + "V27PopulationCapacityDebugScenarios"
                        : "ResearchTreeDebugScenarios + ResearchEquipmentOverhaulDebugScenarios",
                ReviewStatus = before == after ? "unchanged" : "pending",
                ApprovalKey = before != after
                    ? BuildApprovalKey(
                        stableId,
                        metric,
                        afterToken,
                        dependencyFingerprint,
                        sourceDigest,
                        reasonCode,
                        baselineRecordId)
                    : string.Empty,
                DependencyFingerprint = dependencyFingerprint,
                LocalFingerprint = HashText(
                    stableId + "|requiredWork|" + Token(before)),
                SourceDigest = sourceDigest,
                SemanticHash = HashText(stableId + "|" + metric + "|" + afterToken),
                AssetApplied = current == after ? "true" : "false",
                BalanceBaselineRecordId = baselineRecordId
            });
        }
    }

    private static void CaptureSerializedAuthority(
        V27EditorContentSource source,
        BalanceCaptureFactory capture,
        IDictionary<string, string> sourceDigests)
    {
        foreach (ScriptableObject definition in source.AllDefinitions
                     .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal))
        {
            string path = BalanceCanonicalText.ProjectRelativePath(
                AssetDatabase.GetAssetPath(definition));
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (guid.Length != 32)
                throw new InvalidOperationException($"Missing asset GUID: {path}");
            string stableId = "asset:" + guid;
            string domain = ResolveDomain(path, definition.GetType().Name);
            string sourceDigest = GetSourceDigest(path, sourceDigests);
            SerializedObject serialized = new SerializedObject(definition);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = ShouldEnterSerializedChildren(property);
                if (property.propertyPath == "m_Script"
                    || !TryGetSerializedToken(property, out string value, out string unit))
                {
                    continue;
                }
                string metric = "serialized:" + HashText(property.propertyPath).Substring(0, 24);
                string localFingerprint = HashText(
                    definition.GetType().FullName + "|" + property.propertyPath + "|" + value);
                capture.Capture(new BalanceMetricCaptureRequest
                {
                    Domain = domain,
                    DefinitionKind = "serialized-property",
                    StableId = stableId,
                    Metric = metric,
                    Unit = unit,
                    Before = value,
                    After = value,
                    AuthoredRoundedValue = value,
                    PercentDelta = "0",
                    ExactFormula = "unchanged audit authority",
                    BeforeBom = "N/A",
                    AfterBom = "N/A",
                    BeforeDirectWu = "N/A",
                    AfterDirectWu = "N/A",
                    BeforeBomEwu = "N/A",
                    AfterBomEwu = "N/A",
                    BeforeLaborDensity = "N/A",
                    AfterLaborDensity = "N/A",
                    UpstreamOnlyAfter = value,
                    InheritedDelta = "0",
                    RawLocalDelta = "0",
                    LocalQuantizationBoundaryCount = 0,
                    DownstreamConsumerCount = "0",
                    DependencyIds = Array.Empty<string>(),
                    RootCauseIds = Array.Empty<string>(),
                    AnomalyDisposition = "none",
                    ReasonCode = "authority-capture",
                    ReasonDetail = definition.GetType().FullName,
                    SourceAuthority = path,
                    SourcePropertyPath = property.propertyPath,
                    ExecutionRoute = "catalog-authority",
                    SaveAuthority = "Unity ScriptableObject YAML",
                    VerificationEvidence = "serialized-property-capture",
                    ReviewStatus = "captured",
                    ApprovalKey = string.Empty,
                    DependencyFingerprint = HashText(string.Empty),
                    LocalFingerprint = localFingerprint,
                    SourceDigest = sourceDigest,
                    SemanticHash = localFingerprint,
                    AssetApplied = "false",
                    BalanceBaselineRecordId = BaselineRecordId
                });
            }
        }
    }

    private static void CaptureItemValues(
        IEnumerable<ItemDefinitionSO> definitions,
        EmbeddedWorkValueSnapshot before,
        V27EmbeddedWorkValueSnapshot after,
        IMaterialEconomicProfileCatalog materialProfiles,
        ISet<string> semanticRootIds,
        IReadOnlyDictionary<string, int> downstream,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests,
        ICollection<string> integrityFailures,
        IReadOnlyDictionary<string, string> historicalBeforeValues,
        IReadOnlyDictionary<string, string> previouslyApprovedAfterValues,
        bool allowApprovalRefresh)
    {
        foreach (ItemDefinitionSO definition in definitions)
        {
            if (!after.Items.TryGetValue(definition.ItemId, out V27ItemValue value)
                || !before.TryGetItemWork(definition.ItemId, out float beforeFloat))
            {
                continue;
            }
            string path = AssetDatabase.GetAssetPath(definition);
            string sourceDigest = GetSourceDigest(path, sourceDigests);
            decimal beforeDecimal = BalanceCanonicalText.DecimalFromFiniteFloat(
                beforeFloat,
                $"item:{definition.ItemId}:V23EWU");
            long beforeMilli = V27EwuQuantizer.QuantizeInputDebit(beforeDecimal).MilliEwu;
            int downstreamCount = downstream.TryGetValue(
                definition.ItemId,
                out int count)
                ? count
                : 0;
            bool acquisitionEmitsRootCritical =
                ClassifyItemMetricSeverity(
                    definition,
                    beforeMilli,
                    value.AcquisitionCost.MilliEwu,
                    downstreamCount) == BalanceAnomalySeverity.Critical
                && !semanticRootIds.Contains(value.SelectedSourceId);
            CaptureItemMetric(
                definition,
                "acquisition-cost",
                beforeMilli,
                value.AcquisitionCost.MilliEwu,
                value.SelectedSourceId,
                semanticRootIds,
                acquisitionEmitsRootCritical,
                downstreamCount,
                path,
                sourceDigest,
                capture,
                anomalies);
            decimal beforeRetention = BalanceCanonicalText.DecimalFromFiniteFloat(
                materialProfiles.GetSalvageRetention(definition.ItemId),
                $"item:{definition.ItemId}:V23 salvage retention");
            long beforeRecoverable = V27EwuQuantizer.MultiplyOutputCredit(
                EwuAmount.FromMilliEwu(beforeMilli),
                beforeRetention).MilliEwu;
            CaptureItemMetric(
                definition,
                "recoverable-value",
                beforeRecoverable,
                value.RecoverableValue.MilliEwu,
                value.SelectedSourceId,
                semanticRootIds,
                acquisitionEmitsRootCritical,
                downstreamCount,
                path,
                sourceDigest,
                capture,
                anomalies);
            CaptureItemMarketMetrics(
                definition,
                beforeFloat,
                value,
                path,
                sourceDigest,
                capture,
                anomalies,
                integrityFailures,
                historicalBeforeValues,
                previouslyApprovedAfterValues,
                allowApprovalRefresh);
        }
    }

    private static void CaptureItemMarketMetrics(
        ItemDefinitionSO definition,
        float beforeEwu,
        V27ItemValue value,
        string path,
        string sourceDigest,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        ICollection<string> integrityFailures,
        IReadOnlyDictionary<string, string> historicalBeforeValues,
        IReadOnlyDictionary<string, string> previouslyApprovedAfterValues,
        bool allowApprovalRefresh)
    {
        const string AppraisedValuablesId = "offense:appraised-valuables";
        bool appraised = string.Equals(
            definition.ItemId,
            AppraisedValuablesId,
            StringComparison.Ordinal);
        int formulaBeforeUnitPrice = ResolveV23MarketUnitPrice(
            definition.ItemId,
            beforeEwu);
        long marketSaleValue = V27EwuQuantizer.MultiplyOutputCredit(
            value.AcquisitionCost,
            (decimal)GoldEconomyBalanceRules.TargetExternalSaleRecovery).MilliEwu;
        long afterPriceLong = ResolveV27MarketUnitPrice(
            definition.ItemId,
            value.AcquisitionCost.MilliEwu,
            marketSaleValue);
        if (afterPriceLong > int.MaxValue)
        {
            integrityFailures.Add(
                $"V27 unit price overflow: {definition.ItemId}={afterPriceLong}.");
            return;
        }

        int afterUnitPrice = (int)afterPriceLong;
        int currentUnitPrice = definition.UnitPrice;
        string stableId = RawStableId(definition, "itemId");
        string historicalPriceKey =
            V27BalanceAssetApplication.BuildHistoricalBeforeKey(
                stableId,
                "authored-unit-price-gold");
        bool hasHistoricalPrice = historicalBeforeValues.TryGetValue(
                historicalPriceKey,
                out string historicalPriceToken);
        int beforeUnitPrice = hasHistoricalPrice
            ? int.Parse(
                historicalPriceToken,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture)
            : formulaBeforeUnitPrice;
        bool previouslyApprovedCurrentPrice = hasHistoricalPrice
            && previouslyApprovedAfterValues.TryGetValue(
                historicalPriceKey,
                out string approvedPriceAfter)
            && string.Equals(
                approvedPriceAfter,
                currentUnitPrice.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        MarketAuthorityState priceAuthorityState = ClassifyMarketAuthority(
            hasHistoricalPrice,
            previouslyApprovedCurrentPrice,
            currentUnitPrice == formulaBeforeUnitPrice,
            currentUnitPrice == afterUnitPrice,
            currentUnitPrice == beforeUnitPrice);
        bool splitPriceCandidate = priceAuthorityState
            is MarketAuthorityState.PreviouslyApprovedApplied
            or MarketAuthorityState.MissingProvenance;
        if (priceAuthorityState == MarketAuthorityState.UnauthorizedDrift)
        {
            integrityFailures.Add(
                $"V27 unit price authority drift: {definition.ItemId}; "
                + $"Before={beforeUnitPrice}, V27={afterUnitPrice}, current={currentUnitPrice}.");
        }
        string dependencyFingerprint = HashText(value.SelectedSourceId);
        string approvalSourceDigest = definition is ResourceItemDefinitionSO
            ? GetApprovalSourceDigest(path, "unitPrice", "saleRate")
            : GetApprovalSourceDigest(path, "unitPrice");
        string beforeToken = beforeUnitPrice.ToString(CultureInfo.InvariantCulture);
        string afterToken = afterUnitPrice.ToString(CultureInfo.InvariantCulture);
        string currentToken = currentUnitPrice.ToString(CultureInfo.InvariantCulture);
        string authorityBeforeToken = splitPriceCandidate
            ? previouslyApprovedCurrentPrice ? beforeToken : currentToken
            : beforeToken;
        string authorityAfterToken = splitPriceCandidate ? currentToken : afterToken;
        int authorityBeforePrice = int.Parse(
            authorityBeforeToken,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture);
        int authorityAfterPrice = int.Parse(
            authorityAfterToken,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture);
        const string priceReason = "v27-market-acquisition-input-ceil";
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = ResolveDomain(path, definition.GetType().Name),
            DefinitionKind = "item-market",
            StableId = stableId,
            Metric = "authored-unit-price-gold",
            Unit = "gold",
            Before = authorityBeforeToken,
            After = authorityAfterToken,
            AuthoredRoundedValue = authorityAfterToken,
            PercentDelta = Token(PercentDelta(authorityBeforePrice, authorityAfterPrice)),
            ExactFormula = appraised
                ? "max(1,floor(RecoverableValue/3000mEWU-per-gold))"
                : "max(1,ceil(AcquisitionCost/3000mEWU-per-gold))",
            BeforeBom = "N/A",
            AfterBom = "N/A",
            BeforeDirectWu = "N/A",
            AfterDirectWu = "N/A",
            BeforeBomEwu = "N/A",
            AfterBomEwu = "N/A",
            BeforeLaborDensity = "N/A",
            AfterLaborDensity = "N/A",
            UpstreamOnlyAfter = authorityAfterToken,
            InheritedDelta = checked(authorityAfterPrice - authorityBeforePrice).ToString(
                CultureInfo.InvariantCulture),
            RawLocalDelta = "0",
            LocalQuantizationBoundaryCount = 1,
            DownstreamConsumerCount = "1",
            DependencyIds = new[] { stableId },
            RootCauseIds = new[] { stableId },
            AnomalyDisposition = "none",
            ReasonCode = priceReason,
            ReasonDetail = appraised
                ? "Sale-only appraised valuables use the recoverable output credit and floor."
                : "Internal item debit is derived from V27 acquisition mEWU and rounded upward.",
            SourceAuthority = path,
            SourcePropertyPath = "unitPrice",
            ExecutionRoute = "ItemDefinitionSO.UnitPrice->shop/procurement/market ledger",
            SaveAuthority = "ItemDefinitionSO Unity YAML",
            VerificationEvidence = "V27 market formula audit; consumer coherence pending",
            ReviewStatus = splitPriceCandidate
                ? priceAuthorityState == MarketAuthorityState.PreviouslyApprovedApplied
                    ? "applied"
                    : "provenance-missing"
                : currentUnitPrice == afterUnitPrice ? "implemented" : "pending",
            ApprovalKey = authorityBeforePrice == authorityAfterPrice
                    || (splitPriceCandidate && !previouslyApprovedCurrentPrice)
                ? string.Empty
                : BuildApprovalKey(
                    stableId,
                    "authored-unit-price-gold",
                    authorityAfterToken,
                    dependencyFingerprint,
                    approvalSourceDigest,
                    priceReason,
                    MarketBaselineRecordId),
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = HashText(
                definition.ItemId + "|unitPrice|" + value.SelectedSourceId),
            SourceDigest = approvalSourceDigest,
            SemanticHash = BuildMarketAuthoritySemanticHash(
                stableId,
                "authored-unit-price-gold",
                authorityAfterToken),
            AssetApplied = currentUnitPrice == authorityAfterPrice ? "true" : "false",
            BalanceBaselineRecordId = MarketBaselineRecordId
        });
        if (splitPriceCandidate)
        {
            CaptureMarketRecalibrationCandidate(
                capture,
                anomalies,
                stableId,
                "item-market",
                "authored-unit-price-gold",
                "gold",
                currentToken,
                afterToken,
                appraised
                    ? "max(1,floor(RecoverableValue/3000mEWU-per-gold))"
                    : "max(1,ceil(AcquisitionCost/3000mEWU-per-gold))",
                path,
                "unitPrice",
                value.SelectedSourceId,
                dependencyFingerprint,
                approvalSourceDigest,
                "ItemDefinitionSO.UnitPrice->shop/procurement/market ledger",
                previouslyApprovedCurrentPrice);
        }

        if (definition is ResourceItemDefinitionSO resource)
        {
            CaptureItemMarketSaleRate(
                resource,
                beforeEwu,
                beforeUnitPrice,
                afterUnitPrice,
                marketSaleValue,
                path,
                approvalSourceDigest,
                stableId,
                dependencyFingerprint,
                capture,
                anomalies,
                integrityFailures,
                historicalBeforeValues,
                previouslyApprovedAfterValues,
                splitPriceCandidate,
                allowApprovalRefresh);
        }
    }

    private static void CaptureItemMarketSaleRate(
        ResourceItemDefinitionSO definition,
        float beforeEwu,
        int beforeUnitPrice,
        int afterUnitPrice,
        long marketSaleValue,
        string path,
        string approvalSourceDigest,
        string stableId,
        string dependencyFingerprint,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        ICollection<string> integrityFailures,
        IReadOnlyDictionary<string, string> historicalBeforeValues,
        IReadOnlyDictionary<string, string> previouslyApprovedAfterValues,
        bool splitPriceCandidate,
        bool allowApprovalRefresh)
    {
        bool appraised = string.Equals(
            definition.ItemId,
            "offense:appraised-valuables",
            StringComparison.Ordinal);
        bool authoredExcluded = IsAutomaticSaleExcluded(definition.ItemId);
        float formulaBeforeRate = ResolveV23MarketSaleRate(
            definition.ItemId,
            beforeEwu,
            beforeUnitPrice);
        float afterRate = ResolveV27MarketSaleRate(
            definition.ItemId,
            afterUnitPrice,
            marketSaleValue);
        float currentRate = definition.MarketSaleRate;
        string formulaBeforeToken = ((double)formulaBeforeRate).ToString(
            "R",
            CultureInfo.InvariantCulture);
        string afterToken = ((double)afterRate).ToString("R", CultureInfo.InvariantCulture);
        string currentToken = ((double)currentRate).ToString(
            "R",
            CultureInfo.InvariantCulture);
        string historicalKey = V27BalanceAssetApplication.BuildHistoricalBeforeKey(
            stableId,
            "authored-market-sale-rate");
        bool hasHistoricalBefore = historicalBeforeValues.TryGetValue(
            historicalKey,
            out string historicalBeforeToken);
        bool previouslyApprovedCurrentRate = hasHistoricalBefore
            && previouslyApprovedAfterValues.TryGetValue(
                historicalKey,
                out string approvedRateAfter)
            && string.Equals(
                approvedRateAfter,
                currentToken,
                StringComparison.Ordinal);
        float historicalBeforeRate = hasHistoricalBefore
            ? float.Parse(
                historicalBeforeToken,
                NumberStyles.Float,
                CultureInfo.InvariantCulture)
            : formulaBeforeRate;
        MarketAuthorityState rateAuthorityState = ClassifyMarketAuthority(
            hasHistoricalBefore,
            previouslyApprovedCurrentRate,
            AreSameOrAdjacentNonNegativeFloats(currentRate, formulaBeforeRate),
            currentRate == afterRate,
            currentRate == historicalBeforeRate);
        bool splitRateCandidate = rateAuthorityState
            is MarketAuthorityState.PreviouslyApprovedApplied
            or MarketAuthorityState.MissingProvenance;
        string beforeToken = hasHistoricalBefore
            ? historicalBeforeToken
            : rateAuthorityState == MarketAuthorityState.MissingProvenance
                ? currentToken
                : rateAuthorityState == MarketAuthorityState.LegacyReconstructedBaseline
                    ? currentToken
                    : formulaBeforeToken;
        float beforeRate = float.Parse(
            beforeToken,
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        if (rateAuthorityState == MarketAuthorityState.UnauthorizedDrift)
        {
            integrityFailures.Add(
                $"V27 market sale-rate authority drift: {definition.ItemId}; "
                + $"V23={beforeToken}, V27={afterToken}, current={currentToken}.");
        }
        if (authoredExcluded && currentRate != 0f)
        {
            integrityFailures.Add(
                $"V27 automatic-sale exclusion has a non-zero rate: "
                + $"{definition.ItemId}={currentToken}.");
        }
        if (appraised && currentRate != 1f)
        {
            integrityFailures.Add(
                $"V27 appraised valuables require saleRate=1: {currentToken}.");
        }
        string propertyPath = FindUniqueSerializedLeafPath(
            definition,
            "saleRate",
            SerializedPropertyType.Float);
        string authorityBeforeToken = splitRateCandidate
            ? previouslyApprovedCurrentRate ? beforeToken : currentToken
            : beforeToken;
        string authorityAfterToken = splitRateCandidate ? currentToken : afterToken;
        float authorityBeforeRate = float.Parse(
            authorityBeforeToken,
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        float authorityAfterRate = float.Parse(
            authorityAfterToken,
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        const string reason = "v27-market-sale-rate-output-floor";
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = ResolveDomain(path, definition.GetType().Name),
            DefinitionKind = "item-market",
            StableId = stableId,
            Metric = "authored-market-sale-rate",
            Unit = "ratio",
            Before = authorityBeforeToken,
            After = authorityAfterToken,
            AuthoredRoundedValue = authorityAfterToken,
            PercentDelta = Token(PercentDelta(
                (decimal)authorityBeforeRate,
                (decimal)authorityAfterRate)),
            ExactFormula = "max float rate where floor(unitPrice*rate*3000mEWU)<=floor(AcquisitionCost*0.60)",
            BeforeBom = "N/A",
            AfterBom = "N/A",
            BeforeDirectWu = "N/A",
            AfterDirectWu = "N/A",
            BeforeBomEwu = "N/A",
            AfterBomEwu = "N/A",
            BeforeLaborDensity = "N/A",
            AfterLaborDensity = "N/A",
            UpstreamOnlyAfter = authorityAfterToken,
            InheritedDelta = Token(
                (decimal)authorityAfterRate - (decimal)authorityBeforeRate),
            RawLocalDelta = "0",
            LocalQuantizationBoundaryCount = 1,
            DownstreamConsumerCount = "1",
            DependencyIds = new[] { stableId },
            RootCauseIds = new[] { stableId },
            AnomalyDisposition = "none",
            ReasonCode = reason,
            ReasonDetail = authoredExcluded
                ? "The authored item remains excluded from automatic market sale."
                : appraised
                    ? "Appraised valuables remain a sale-only output with rate 1 after their unit price is floored."
                    : "The sale rate is rounded downward at float precision so integer unit-price Ceil cannot exceed the 60% external recovery target.",
            SourceAuthority = path,
            SourcePropertyPath = propertyPath,
            ExecutionRoute = "ResourceItemDefinitionSO.MarketSaleRate->ResourceStockPolicyRuntime",
            SaveAuthority = "ResourceItemDefinitionSO MarketItemFeature Unity YAML",
            VerificationEvidence = "V27 sale-credit floor audit; physical market PlayMode pending",
            ReviewStatus = splitRateCandidate
                ? rateAuthorityState == MarketAuthorityState.PreviouslyApprovedApplied
                    ? "applied"
                    : "provenance-missing"
                : currentRate == afterRate ? "implemented" : "pending",
            ApprovalKey = authorityBeforeRate == authorityAfterRate
                    || (splitRateCandidate && !previouslyApprovedCurrentRate)
                ? string.Empty
                : BuildApprovalKey(
                    stableId,
                    "authored-market-sale-rate",
                    authorityAfterToken,
                    dependencyFingerprint,
                    approvalSourceDigest,
                    reason,
                    MarketBaselineRecordId),
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = HashText(
                definition.ItemId + "|saleRate|" + authorityBeforeToken),
            SourceDigest = approvalSourceDigest,
            SemanticHash = BuildMarketAuthoritySemanticHash(
                stableId,
                "authored-market-sale-rate",
                authorityAfterToken),
            AssetApplied = currentRate == authorityAfterRate ? "true" : "false",
            BalanceBaselineRecordId = MarketBaselineRecordId
        });
        if (splitRateCandidate)
        {
            CaptureMarketRecalibrationCandidate(
                capture,
                anomalies,
                stableId,
                "item-market",
                "authored-market-sale-rate",
                "ratio",
                currentToken,
                afterToken,
                "max float rate where floor(unitPrice*rate*3000mEWU)<=floor(AcquisitionCost*0.60)",
                path,
                propertyPath,
                stableId,
                dependencyFingerprint,
                approvalSourceDigest,
                "ResourceItemDefinitionSO.MarketSaleRate->ResourceStockPolicyRuntime",
                previouslyApprovedCurrentRate);
        }

        long beforeCredit = checked((long)Math.Floor(
            beforeUnitPrice * (decimal)beforeRate * 3000m));
        long currentCredit = checked((long)Math.Floor(
            definition.UnitPrice * (decimal)currentRate * 3000m));
        long afterCredit = checked((long)Math.Floor(
            afterUnitPrice * (decimal)afterRate * 3000m));
        bool splitCreditCandidate = splitPriceCandidate || splitRateCandidate;
        long authorityBeforeCredit = splitCreditCandidate ? currentCredit : beforeCredit;
        long authorityAfterCredit = splitCreditCandidate ? currentCredit : afterCredit;
        long marketSaleCreditCap = marketSaleValue;
        bool creditExceedsTarget = !authoredExcluded
            && afterCredit > marketSaleCreditCap;
        if (creditExceedsTarget)
        {
            integrityFailures.Add(
                $"V27 market sale credit exceeds the 60% acquisition cap: "
                + $"{definition.ItemId}; credit={afterCredit}, cap={marketSaleCreditCap}.");
        }
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = ResolveDomain(path, definition.GetType().Name),
            DefinitionKind = "item-market",
            StableId = stableId,
            Metric = "market-sale-credit",
            Unit = "mEWU",
            Before = authorityBeforeCredit.ToString(CultureInfo.InvariantCulture),
            After = authorityAfterCredit.ToString(CultureInfo.InvariantCulture),
            AuthoredRoundedValue = authorityAfterCredit.ToString(CultureInfo.InvariantCulture),
            PercentDelta = Token(PercentDelta(authorityBeforeCredit, authorityAfterCredit)),
            ExactFormula = "floor(authoredUnitPrice*authoredSaleRate*3000mEWU)",
            BeforeBom = "N/A",
            AfterBom = "N/A",
            BeforeDirectWu = "N/A",
            AfterDirectWu = "N/A",
            BeforeBomEwu = "N/A",
            AfterBomEwu = "N/A",
            BeforeLaborDensity = "N/A",
            AfterLaborDensity = "N/A",
            UpstreamOnlyAfter = authorityAfterCredit.ToString(CultureInfo.InvariantCulture),
            InheritedDelta = checked(authorityAfterCredit - authorityBeforeCredit).ToString(
                CultureInfo.InvariantCulture),
            RawLocalDelta = "0",
            LocalQuantizationBoundaryCount = 1,
            DownstreamConsumerCount = "1",
            DependencyIds = new[] { stableId },
            RootCauseIds = new[] { stableId },
            AnomalyDisposition = creditExceedsTarget ? "critical" : "none",
            ReasonCode = "v27-market-sale-credit-output-floor",
            ReasonDetail = "The float sale rate and the settlement credit both round toward the economy sink; the result cannot exceed 60% of acquisition value.",
            SourceAuthority = path,
            SourcePropertyPath = "unitPrice|" + propertyPath,
            ExecutionRoute = "ResourceStockPolicyRuntime sale settlement",
            SaveAuthority = "physical sale buffer + treasury transaction ledger",
            VerificationEvidence = "V27 market formula audit; physical market PlayMode pending",
            ReviewStatus = splitCreditCandidate
                ? "observed-live-derived"
                : creditExceedsTarget ? "blocked" : "review",
            ApprovalKey = string.Empty,
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = HashText(
                definition.ItemId + "|market-sale-credit|" + authorityBeforeCredit),
            SourceDigest = approvalSourceDigest,
            SemanticHash = HashText(
                definition.ItemId + "|market-sale-credit|" + authorityAfterCredit),
            AssetApplied = "true",
            BalanceBaselineRecordId = MarketBaselineRecordId
        });
        if (splitCreditCandidate)
        {
            string candidateMetric = MarketDerivedRecalibrationCandidateMetricPrefix
                + "market-sale-credit";
            capture.Capture(new BalanceMetricCaptureRequest
            {
                Domain = ResolveDomain(path, definition.GetType().Name),
                DefinitionKind = "item-market-derived-recalibration-candidate",
                StableId = stableId,
                Metric = candidateMetric,
                Unit = "mEWU",
                Before = currentCredit.ToString(CultureInfo.InvariantCulture),
                After = afterCredit.ToString(CultureInfo.InvariantCulture),
                AuthoredRoundedValue = afterCredit.ToString(CultureInfo.InvariantCulture),
                PercentDelta = Token(PercentDelta(currentCredit, afterCredit)),
                ExactFormula = "floor(candidateUnitPrice*candidateSaleRate*3000mEWU)",
                BeforeBom = "N/A",
                AfterBom = "N/A",
                BeforeDirectWu = "N/A",
                AfterDirectWu = "N/A",
                BeforeBomEwu = "N/A",
                AfterBomEwu = "N/A",
                BeforeLaborDensity = "N/A",
                AfterLaborDensity = "N/A",
                UpstreamOnlyAfter = afterCredit.ToString(CultureInfo.InvariantCulture),
                InheritedDelta = checked(afterCredit - currentCredit).ToString(
                    CultureInfo.InvariantCulture),
                RawLocalDelta = "0",
                LocalQuantizationBoundaryCount = 1,
                DownstreamConsumerCount = "review-only-derived",
                DependencyIds = new[] { stableId },
                RootCauseIds = new[] { stableId },
                AnomalyDisposition = "collapsed-inherited",
                ReasonCode = "derived-from-unresolved-market-authority",
                ReasonDetail = "This sale credit is derived only from unresolved price or sale-rate candidates. It cannot become live or independently approved.",
                SourceAuthority = path,
                SourcePropertyPath = "unitPrice|" + propertyPath,
                ExecutionRoute = "review-only derived projection; authority route=ResourceStockPolicyRuntime sale settlement",
                SaveAuthority = "derived market recalibration projection; no independent write authority",
                VerificationEvidence = "V27 market authority/candidate separation audit",
                ReviewStatus = "pending-upstream-review",
                ApprovalKey = string.Empty,
                DependencyFingerprint = dependencyFingerprint,
                LocalFingerprint = HashText(
                    definition.ItemId + "|" + candidateMetric + "|" + currentCredit),
                SourceDigest = approvalSourceDigest,
                SemanticHash = HashText(
                    definition.ItemId + "|" + candidateMetric + "|" + afterCredit),
                AssetApplied = "false",
                BalanceBaselineRecordId = MarketBaselineRecordId
            });
        }
    }

    private static float ResolveV23MarketSaleRate(
        string itemId,
        float beforeEwu,
        int beforeUnitPrice)
    {
        if (IsAutomaticSaleExcluded(itemId))
            return 0f;
        if (string.Equals(itemId, "offense:appraised-valuables", StringComparison.Ordinal))
            return 1f;
        float targetSaleGold = beforeEwu
            * GoldEconomyBalanceRules.GoldPerEmbeddedWorkUnit
            * GoldEconomyBalanceRules.TargetExternalSaleRecovery;
        return Mathf.Clamp01(targetSaleGold / beforeUnitPrice);
    }

    private static int ResolveV23MarketUnitPrice(string itemId, float beforeEwu)
    {
        bool appraised = string.Equals(
            itemId,
            "offense:appraised-valuables",
            StringComparison.Ordinal);
        return appraised
            ? Mathf.Max(1, Mathf.RoundToInt(
                beforeEwu
                * GoldEconomyBalanceRules.GoldPerEmbeddedWorkUnit
                * GoldEconomyBalanceRules.TargetExternalSaleRecovery))
            : Mathf.Max(1, Mathf.RoundToInt(
                beforeEwu * GoldEconomyBalanceRules.GoldPerEmbeddedWorkUnit));
    }

    private static long ResolveV27MarketUnitPrice(
        string itemId,
        long acquisitionMilliEwu,
        long marketSaleValue)
    {
        bool appraised = string.Equals(
            itemId,
            "offense:appraised-valuables",
            StringComparison.Ordinal);
        return appraised
            ? Math.Max(1L, marketSaleValue / 3000L)
            : Math.Max(1L, DivideCeilPositive(acquisitionMilliEwu, 3000L));
    }

    private static float ResolveV27MarketSaleRate(
        string itemId,
        int afterUnitPrice,
        long marketSaleValue)
    {
        if (IsAutomaticSaleExcluded(itemId))
            return 0f;
        if (string.Equals(itemId, "offense:appraised-valuables", StringComparison.Ordinal))
            return 1f;
        if (afterUnitPrice <= 0 || marketSaleValue <= 0L)
            return 0f;

        decimal denominator = checked(afterUnitPrice * 3000m);
        float candidate = Mathf.Clamp01((float)(marketSaleValue / denominator));
        while (candidate > 0f
               && afterUnitPrice * (decimal)candidate * 3000m > marketSaleValue)
        {
            candidate = PreviousNonNegativeFloat(candidate);
        }
        while (candidate < 1f)
        {
            float next = NextNonNegativeFloat(candidate);
            if (next <= candidate
                || afterUnitPrice * (decimal)next * 3000m > marketSaleValue)
            {
                break;
            }
            candidate = next;
        }
        return candidate;
    }

    private static float PreviousNonNegativeFloat(float value)
    {
        if (value <= 0f)
            return 0f;
        int bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        return BitConverter.ToSingle(BitConverter.GetBytes(bits - 1), 0);
    }

    private static float NextNonNegativeFloat(float value)
    {
        if (value < 0f || float.IsNaN(value) || float.IsPositiveInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        int bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        return BitConverter.ToSingle(BitConverter.GetBytes(bits + 1), 0);
    }

    private static bool AreSameOrAdjacentNonNegativeFloats(float left, float right)
    {
        if (left == right)
            return true;
        if (left < 0f || right < 0f || !float.IsFinite(left) || !float.IsFinite(right))
            return false;
        int leftBits = BitConverter.ToInt32(BitConverter.GetBytes(left), 0);
        int rightBits = BitConverter.ToInt32(BitConverter.GetBytes(right), 0);
        return Math.Abs((long)leftBits - rightBits) <= 1L;
    }

    private static MarketAuthorityState ClassifyMarketAuthority(
        bool hasHistorical,
        bool previouslyApprovedCurrent,
        bool currentMatchesLegacy,
        bool currentMatchesCandidate,
        bool currentMatchesHistoricalBefore)
    {
        if (currentMatchesCandidate)
            return MarketAuthorityState.Implemented;
        if (previouslyApprovedCurrent)
            return MarketAuthorityState.PreviouslyApprovedApplied;
        if (hasHistorical)
        {
            return currentMatchesHistoricalBefore
                ? MarketAuthorityState.LegacyReconstructedBaseline
                : MarketAuthorityState.UnauthorizedDrift;
        }
        return currentMatchesLegacy
            ? MarketAuthorityState.LegacyReconstructedBaseline
            : MarketAuthorityState.MissingProvenance;
    }

    private static bool IsAutomaticSaleExcluded(string itemId) => itemId switch
    {
        PhysicalItemIds.EquipmentModule => true,
        EquipmentProgressionItemIds.LineageSeal => true,
        "offense:unappraised-loot" => true,
        "seed-lot:bloodleaf" => true,
        "seed-lot:cave-mushroom" => true,
        "seed-lot:dreamleaf" => true,
        "seed-lot:ember-cotton" => true,
        "seed-lot:ember-root" => true,
        "seed-lot:frost-flax" => true,
        "seed-lot:mire-reed" => true,
        "seed-lot:moonflower" => true,
        "seed-lot:night-grape" => true,
        "seed-lot:shade-fiber" => true,
        "seed-lot:spore-hemp" => true,
        "seed-lot:twilight-grain" => true,
        _ => false
    };

    private static void CaptureItemMarketConsumers(
        V27EditorContentSource source,
        IReadOnlyCollection<ItemDefinitionSO> items,
        EmbeddedWorkValueSnapshot before,
        V27EmbeddedWorkValueSnapshot after,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests,
        ICollection<string> integrityFailures,
        IReadOnlyDictionary<string, string> historicalBeforeValues,
        IReadOnlyDictionary<string, string> previouslyApprovedAfterValues,
        bool allowApprovalRefresh)
    {
        Dictionary<string, ItemDefinitionSO> itemById = items.ToDictionary(
            value => value.ItemId,
            StringComparer.Ordinal);
        Dictionary<string, int> beforePrices = new Dictionary<string, int>(
            StringComparer.Ordinal);
        Dictionary<string, int> afterPrices = new Dictionary<string, int>(
            StringComparer.Ordinal);
        foreach (ItemDefinitionSO item in items.OrderBy(value => value.ItemId, StringComparer.Ordinal))
        {
            if (!before.TryGetItemWork(item.ItemId, out float beforeEwu)
                || !after.Items.TryGetValue(item.ItemId, out V27ItemValue afterValue))
            {
                continue;
            }
            long marketSaleValue = V27EwuQuantizer.MultiplyOutputCredit(
                afterValue.AcquisitionCost,
                (decimal)GoldEconomyBalanceRules.TargetExternalSaleRecovery).MilliEwu;
            string stableId = RawStableId(item, "itemId");
            string historicalKey =
                V27BalanceAssetApplication.BuildHistoricalBeforeKey(
                    stableId,
                    "authored-unit-price-gold");
            beforePrices[item.ItemId] = historicalBeforeValues.TryGetValue(
                    historicalKey,
                    out string historicalPriceToken)
                ? int.Parse(
                    historicalPriceToken,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture)
                : ResolveV23MarketUnitPrice(item.ItemId, beforeEwu);
            afterPrices[item.ItemId] = checked((int)ResolveV27MarketUnitPrice(
                item.ItemId,
                afterValue.AcquisitionCost.MilliEwu,
                marketSaleValue));
        }

        foreach (SaleItem saleItem in Resources.LoadAll<SaleItem>("SO/Stock/Item")
                     .Where(value => value != null)
                     .OrderBy(value => value.id))
        {
            string itemId = saleItem.ItemDefinitionId.Value;
            if (!beforePrices.TryGetValue(itemId, out int beforePrice)
                || !afterPrices.TryGetValue(itemId, out int afterPrice))
            {
                integrityFailures.Add(
                    $"V27 retail offer references an unvalued item: {saleItem.name}:{itemId}.");
                continue;
            }
            int beforeCost = GoldEconomyBalanceRules.CalculateRetailBasePrice(beforePrice);
            int afterCost = GoldEconomyBalanceRules.CalculateRetailBasePrice(afterPrice);
            string path = AssetDatabase.GetAssetPath(saleItem);
            GetSourceDigest(path, sourceDigests);
            CaptureMarketConsumerPatch(
                capture,
                anomalies,
                integrityFailures,
                path,
                GetApprovalSourceDigest(path, "cost"),
                "retail-offer:" + saleItem.id.ToString(CultureInfo.InvariantCulture),
                "retail-offer",
                "authored-retail-cost-gold",
                "gold",
                beforeCost.ToString(CultureInfo.InvariantCulture),
                afterCost.ToString(CultureInfo.InvariantCulture),
                saleItem.cost.ToString(CultureInfo.InvariantCulture),
                "cost",
                "ceil(itemUnitPrice*1.20)",
                "v27-market-retail-input-ceil",
                itemId,
                "SaleItem.cost->FacilityShop purchase debit",
                historicalBeforeValues,
                previouslyApprovedAfterValues,
                saleItem.cost == beforeCost,
                allowApprovalRefresh);
        }

        GameDomainContentCatalogSO domain = source.DomainCatalog;
        List<(AuthoredStockCategoryRecord Record, int Index)> stocks = domain.StockCategories
            .Select((record, index) => (record, index))
            .Where(pair => pair.record != null
                && pair.record.dailyBaseAmount > 0
                && !string.IsNullOrWhiteSpace(pair.record.deliveryItemId))
            .Select(pair => (pair.record, pair.index))
            .ToList();
        string[] stockPropertyPaths = stocks.Select(pair =>
                $"stockCategories.Array.data[{pair.Index}].dailyUnitCost")
            .ToArray();
        string domainPath = AssetDatabase.GetAssetPath(domain);
        GetSourceDigest(domainPath, sourceDigests);
        string stockApprovalDigest = GetApprovalSerializedDigest(domain, stockPropertyPaths);
        foreach ((AuthoredStockCategoryRecord stock, int index) in stocks)
        {
            string itemId = stock.deliveryItemId;
            if (!before.TryGetItemWork(itemId, out float beforeEwu)
                || !after.Items.TryGetValue(itemId, out V27ItemValue afterValue))
            {
                integrityFailures.Add(
                    $"V27 stock category references an unvalued item: {stock.id}:{itemId}.");
                continue;
            }
            float formulaBefore = beforeEwu
                * GoldEconomyBalanceRules.TargetPurchaseGoldPerEmbeddedWorkUnit;
            float afterCost = ResolveV27InputFloat(
                afterValue.AcquisitionCost.MilliEwu / 1000m * 0.45m);
            string stableId = "stock-category:" + stock.id;
            string metric = "authored-daily-unit-cost-gold";
            string formulaBeforeToken = ((double)formulaBefore).ToString(
                "R",
                CultureInfo.InvariantCulture);
            string afterToken = ((double)afterCost).ToString("R", CultureInfo.InvariantCulture);
            string currentToken = ((double)stock.dailyUnitCost).ToString(
                "R",
                CultureInfo.InvariantCulture);
            string historicalKey = V27BalanceAssetApplication.BuildHistoricalBeforeKey(
                stableId,
                metric);
            bool hasHistorical = historicalBeforeValues.TryGetValue(
                historicalKey,
                out string historicalToken);
            string beforeToken = hasHistorical
                ? historicalToken
                : formulaBeforeToken;
            CaptureMarketConsumerPatch(
                capture,
                anomalies,
                integrityFailures,
                domainPath,
                stockApprovalDigest,
                stableId,
                "stock-procurement",
                metric,
                "gold/item",
                beforeToken,
                afterToken,
                currentToken,
                $"stockCategories.Array.data[{index}].dailyUnitCost",
                "ceil(AcquisitionCostEWU*0.45) at final purchase settlement",
                "v27-market-procurement-input-ceil",
                itemId,
                "AuthoredStockCategoryRecord.dailyUnitCost->StockSupplyService purchase debit",
                historicalBeforeValues,
                previouslyApprovedAfterValues,
                AreSameOrAdjacentNonNegativeFloats(
                    stock.dailyUnitCost,
                    formulaBefore),
                allowApprovalRefresh);
        }

        foreach (GuestRequestDefinitionSO request in source.GetAll<GuestRequestDefinitionSO>()
                     .Where(value => value != null)
                     .OrderBy(value => value.StableId, StringComparer.Ordinal))
        {
            List<V20ItemAmountRequirement> consumed = (request.serviceRequirements?.items
                    ?? new List<V20ItemAmountRequirement>())
                .Where(value => value != null && value.consume)
                .ToList();
            int beforeInternal = 0;
            int afterInternal = 0;
            bool missing = false;
            foreach (V20ItemAmountRequirement requirement in consumed)
            {
                string itemId = requirement.itemDefinitionId?.Trim() ?? string.Empty;
                if (!itemById.ContainsKey(itemId)
                    || !beforePrices.TryGetValue(itemId, out int beforePrice)
                    || !afterPrices.TryGetValue(itemId, out int afterPrice))
                {
                    integrityFailures.Add(
                        $"V27 guest request references an unvalued consumed item: "
                        + $"{request.StableId}:{itemId}.");
                    missing = true;
                    break;
                }
                beforeInternal = checked(beforeInternal + beforePrice * requirement.amount);
                afterInternal = checked(afterInternal + afterPrice * requirement.amount);
            }
            if (missing || beforeInternal <= 0 || afterInternal <= 0)
                continue;
            int moneyIndex = (request.successEffects ?? new List<V20ContentEffect>())
                .FindIndex(value => value != null && value.kind == V20ContentEffectKind.Money);
            if (moneyIndex < 0
                || request.successEffects.Count(value =>
                    value != null && value.kind == V20ContentEffectKind.Money) != 1)
            {
                integrityFailures.Add(
                    $"V27 guest request requires exactly one money reward: {request.StableId}.");
                continue;
            }
            V20ContentEffect money = request.successEffects[moneyIndex];
            int beforeReward = GoldEconomyBalanceRules.CalculatePremiumServiceReward(beforeInternal);
            int afterReward = GoldEconomyBalanceRules.CalculatePremiumServiceReward(afterInternal);
            string path = AssetDatabase.GetAssetPath(request);
            string propertyPath = $"successEffects.Array.data[{moneyIndex}].amount";
            GetSourceDigest(path, sourceDigests);
            CaptureMarketConsumerPatch(
                capture,
                anomalies,
                integrityFailures,
                path,
                GetApprovalSerializedDigest(request, propertyPath),
                request.StableId,
                "guest-request",
                "authored-money-reward-gold",
                "gold",
                beforeReward.ToString(CultureInfo.InvariantCulture),
                afterReward.ToString(CultureInfo.InvariantCulture),
                ((double)money.amount).ToString("R", CultureInfo.InvariantCulture),
                propertyPath,
                "ceil(consumedItemInternalValue/(1-0.25))",
                "v27-market-premium-service-input-ceil",
                string.Join("|", consumed.Select(value => value.itemDefinitionId)),
                "GuestRequestDefinitionSO.successEffects(Money)->campaign reward credit",
                historicalBeforeValues,
                previouslyApprovedAfterValues,
                money.amount == beforeReward,
                allowApprovalRefresh);
        }
    }

    private static void CaptureMarketConsumerPatch(
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        ICollection<string> integrityFailures,
        string path,
        string approvalSourceDigest,
        string stableId,
        string definitionKind,
        string metric,
        string unit,
        string before,
        string after,
        string current,
        string propertyPath,
        string formula,
        string reasonCode,
        string dependencyId,
        string executionRoute,
        IReadOnlyDictionary<string, string> historicalBeforeValues,
        IReadOnlyDictionary<string, string> previouslyApprovedAfterValues,
        bool currentMatchesLegacy,
        bool allowApprovalRefresh)
    {
        string historicalKey = V27BalanceAssetApplication.BuildHistoricalBeforeKey(
            stableId,
            metric);
        bool hasHistorical = historicalBeforeValues.TryGetValue(
            historicalKey,
            out string historicalBefore);
        if (hasHistorical)
            before = historicalBefore;
        bool previouslyApprovedCurrent = hasHistorical
            && previouslyApprovedAfterValues.TryGetValue(
                historicalKey,
                out string approvedAfter)
            && string.Equals(approvedAfter, current, StringComparison.Ordinal);
        MarketAuthorityState authorityState = ClassifyMarketAuthority(
            hasHistorical,
            previouslyApprovedCurrent,
            currentMatchesLegacy,
            string.Equals(current, after, StringComparison.Ordinal),
            string.Equals(current, before, StringComparison.Ordinal));
        bool splitCandidate = authorityState
            is MarketAuthorityState.PreviouslyApprovedApplied
            or MarketAuthorityState.MissingProvenance;
        if (authorityState == MarketAuthorityState.UnauthorizedDrift)
        {
            integrityFailures.Add(
                $"V27 market consumer authority drift: {stableId}:{metric}; "
                + $"Before={before}, After={after}, current={current}.");
        }
        string authorityBefore = splitCandidate
            ? previouslyApprovedCurrent ? before : current
            : before;
        string authorityAfter = splitCandidate ? current : after;
        decimal beforeNumber = decimal.Parse(
            authorityBefore,
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        decimal afterNumber = decimal.Parse(
            authorityAfter,
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        string dependencyFingerprint = HashText(dependencyId);
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = ResolveDomain(path, definitionKind),
            DefinitionKind = definitionKind,
            StableId = stableId,
            Metric = metric,
            Unit = unit,
            Before = authorityBefore,
            After = authorityAfter,
            AuthoredRoundedValue = authorityAfter,
            PercentDelta = Token(PercentDelta(beforeNumber, afterNumber)),
            ExactFormula = formula,
            BeforeBom = "N/A",
            AfterBom = "N/A",
            BeforeDirectWu = "N/A",
            AfterDirectWu = "N/A",
            BeforeBomEwu = "N/A",
            AfterBomEwu = "N/A",
            BeforeLaborDensity = "N/A",
            AfterLaborDensity = "N/A",
            UpstreamOnlyAfter = authorityAfter,
            InheritedDelta = Token(afterNumber - beforeNumber),
            RawLocalDelta = "0",
            LocalQuantizationBoundaryCount = 1,
            DownstreamConsumerCount = "1",
            DependencyIds = new[] { dependencyId },
            RootCauseIds = new[] { dependencyId },
            AnomalyDisposition = "none",
            ReasonCode = reasonCode,
            ReasonDetail = "This downstream consumer changes in the same approval set as its V27 item-value authority.",
            SourceAuthority = path,
            SourcePropertyPath = propertyPath,
            ExecutionRoute = executionRoute,
            SaveAuthority = "Unity YAML authored consumer authority",
            VerificationEvidence = "V27 market consumer coherence audit; live settlement evidence pending",
            ReviewStatus = splitCandidate
                ? authorityState == MarketAuthorityState.PreviouslyApprovedApplied
                    ? "applied"
                    : "provenance-missing"
                : string.Equals(current, after, StringComparison.Ordinal)
                    ? "implemented"
                    : "pending",
            ApprovalKey = string.Equals(authorityBefore, authorityAfter, StringComparison.Ordinal)
                    || (splitCandidate && !previouslyApprovedCurrent)
                ? string.Empty
                : BuildApprovalKey(
                    stableId,
                    metric,
                    authorityAfter,
                    dependencyFingerprint,
                    approvalSourceDigest,
                    reasonCode,
                    MarketBaselineRecordId),
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = HashText(stableId + "|" + metric + "|" + dependencyId),
            SourceDigest = approvalSourceDigest,
            SemanticHash = BuildMarketAuthoritySemanticHash(
                stableId,
                metric,
                authorityAfter),
            AssetApplied = string.Equals(current, authorityAfter, StringComparison.Ordinal)
                ? "true"
                : "false",
            BalanceBaselineRecordId = MarketBaselineRecordId
        });
        if (splitCandidate)
        {
            CaptureMarketRecalibrationCandidate(
                capture,
                anomalies,
                stableId,
                definitionKind,
                metric,
                unit,
                current,
                after,
                formula,
                path,
                propertyPath,
                dependencyId,
                dependencyFingerprint,
                approvalSourceDigest,
                executionRoute,
                previouslyApprovedCurrent);
        }
    }

    private static void CaptureMarketRecalibrationCandidate(
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        string stableId,
        string definitionKind,
        string authorityMetric,
        string unit,
        string current,
        string candidate,
        string formula,
        string path,
        string propertyPath,
        string dependencyId,
        string authorityDependencyFingerprint,
        string sourceDigest,
        string authorityExecutionRoute,
        bool previouslyApprovedCurrent)
    {
        string metric = MarketRecalibrationCandidateMetricPrefix + authorityMetric;
        string reasonCode = previouslyApprovedCurrent
            ? "previous-applied-market-recalibration-review-required"
            : "market-authority-provenance-missing";
        decimal currentNumber = decimal.Parse(
            current,
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        decimal candidateNumber = decimal.Parse(
            candidate,
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = ResolveDomain(path, definitionKind),
            DefinitionKind = definitionKind + "-recalibration-candidate",
            StableId = stableId,
            Metric = metric,
            Unit = unit,
            Before = current,
            After = candidate,
            AuthoredRoundedValue = candidate,
            PercentDelta = Token(PercentDelta(currentNumber, candidateNumber)),
            ExactFormula = formula,
            BeforeBom = "N/A",
            AfterBom = "N/A",
            BeforeDirectWu = "N/A",
            AfterDirectWu = "N/A",
            BeforeBomEwu = "N/A",
            AfterBomEwu = "N/A",
            BeforeLaborDensity = "N/A",
            AfterLaborDensity = "N/A",
            UpstreamOnlyAfter = candidate,
            InheritedDelta = Token(candidateNumber - currentNumber),
            RawLocalDelta = "0",
            LocalQuantizationBoundaryCount = 1,
            DownstreamConsumerCount = "review-only",
            DependencyIds = new[] { dependencyId },
            RootCauseIds = Array.Empty<string>(),
            AnomalyDisposition = "local-critical",
            ReasonCode = reasonCode,
            ReasonDetail = previouslyApprovedCurrent
                ? "The exact previously approved current authority is retained; this newly calculated value requires explicit promotion review."
                : "The exact current authored value is retained as observed Authority, not as an accepted baseline. No surviving exact approval proves its provenance, so this candidate remains an unresolved Critical until explicit review.",
            SourceAuthority = path,
            SourcePropertyPath = propertyPath,
            ExecutionRoute = "review-only:V27 market recalculation->explicit promotion transaction; authority route="
                + authorityExecutionRoute,
            SaveAuthority = "derived market recalibration proposal + explicit review authority",
            VerificationEvidence = "V27 current-authority/recalibration-candidate separation audit",
            ReviewStatus = "pending-explicit-review",
            ApprovalKey = string.Empty,
            DependencyFingerprint = authorityDependencyFingerprint,
            LocalFingerprint = HashText(
                stableId + "|" + metric + "|" + current + "|" + candidate),
            SourceDigest = sourceDigest,
            SemanticHash = HashText(
                stableId + "|" + metric + "|" + candidate),
            AssetApplied = "false",
            BalanceBaselineRecordId = MarketBaselineRecordId
        });
        anomalies.Add(BalanceAnomalyNode.Capture(
            stableId,
            metric,
            BalanceAnomalySeverity.Critical,
            BalanceAnomalyDisposition.RootCritical,
            reasonCode,
            Array.Empty<string>()));
    }

    private static float ResolveV27InputFloat(decimal exactMinimum)
    {
        if (exactMinimum < 0m)
            throw new ArgumentOutOfRangeException(nameof(exactMinimum));
        float candidate = (float)exactMinimum;
        while ((decimal)candidate < exactMinimum)
            candidate = NextNonNegativeFloat(candidate);
        while (candidate > 0f)
        {
            float previous = PreviousNonNegativeFloat(candidate);
            if ((decimal)previous < exactMinimum)
                break;
            candidate = previous;
        }
        return candidate;
    }

    private static string GetApprovalSerializedDigest(
        UnityEngine.Object authority,
        params string[] excludedPropertyPaths)
    {
        if (authority == null)
            throw new ArgumentNullException(nameof(authority));
        HashSet<string> excluded = new HashSet<string>(
            excludedPropertyPaths ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        SerializedObject serialized = new SerializedObject(authority);
        SerializedProperty iterator = serialized.GetIterator();
        StringBuilder canonical = new StringBuilder(4096);
        bool enterChildren = true;
        while (iterator.Next(enterChildren))
        {
            enterChildren = true;
            if (iterator.hasVisibleChildren
                && iterator.propertyType == SerializedPropertyType.Generic)
            {
                continue;
            }
            canonical.Append(iterator.propertyPath);
            canonical.Append('\u001f');
            canonical.Append(iterator.propertyType);
            canonical.Append('\u001f');
            canonical.Append(excluded.Contains(iterator.propertyPath)
                ? "<v27-approved-target>"
                : iterator.contentHash.ToString());
            canonical.Append('\n');
        }
        foreach (string path in excluded)
        {
            if (serialized.FindProperty(path) == null)
            {
                throw new InvalidOperationException(
                    $"V27 approval digest property is missing: {authority.name}:{path}.");
            }
        }
        return HashText(canonical.ToString());
    }

    private static string FindUniqueSerializedLeafPath(
        UnityEngine.Object authority,
        string leafName,
        SerializedPropertyType propertyType)
    {
        SerializedObject serialized = new SerializedObject(authority);
        SerializedProperty iterator = serialized.GetIterator();
        string match = string.Empty;
        bool enterChildren = true;
        while (iterator.Next(enterChildren))
        {
            enterChildren = true;
            if (!string.Equals(iterator.name, leafName, StringComparison.Ordinal)
                || iterator.propertyType != propertyType)
                continue;
            if (match.Length != 0)
                throw new InvalidOperationException(
                    $"Serialized leaf '{leafName}' is ambiguous on {authority.name}.");
            match = iterator.propertyPath;
        }
        return match.Length != 0
            ? match
            : throw new InvalidOperationException(
                $"Serialized leaf '{leafName}' is missing on {authority.name}.");
    }

    private static long DivideCeilPositive(long numerator, long denominator)
    {
        if (numerator < 0L || denominator <= 0L)
            throw new InvalidOperationException(
                $"Positive ceil division requires numerator>=0 and denominator>0: "
                + $"{numerator}/{denominator}.");
        return numerator == 0L ? 0L : checked(1L + (numerator - 1L) / denominator);
    }

    private static void CaptureItemMetric(
        ItemDefinitionSO definition,
        string metric,
        long before,
        long after,
        string selectedSourceId,
        ISet<string> semanticRootIds,
        bool acquisitionEmitsRootCritical,
        int downstream,
        string path,
        string sourceDigest,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies)
    {
        decimal percent = PercentDelta(before, after);
        BalanceAnomalySeverity severity = ClassifyItemMetricSeverity(
            definition,
            before,
            after,
            downstream);
        string stableId = RawStableId(definition, "itemId");
        string[] dependencies = selectedSourceId.StartsWith("external:", StringComparison.Ordinal)
            ? Array.Empty<string>()
            : new[] { selectedSourceId };
        string[] rootCauseIds = ResolveItemMetricRootCauseIds(
            stableId,
            metric,
            selectedSourceId,
            semanticRootIds,
            acquisitionEmitsRootCritical);
        BalanceAnomalyDisposition disposition = severity == BalanceAnomalySeverity.Critical
            ? rootCauseIds.Length > 0
                ? BalanceAttribution.Attribute(
                    before,
                    after,
                    after,
                    localFingerprintIdentical: true,
                    changeOriginatesOnlyUpstream: true,
                    localQuantizationBoundaryCount: 2,
                    rootCauseIds,
                    isCritical: true).Disposition
                : BalanceAnomalyDisposition.RootCritical
            : BalanceAnomalyDisposition.None;
        string fingerprint = HashText(
            metric + "|" + selectedSourceId + "|" + definition.UnitWeight.ToString("R", CultureInfo.InvariantCulture));
        string dependencyFingerprint = HashText(string.Join("|", dependencies));
        string afterToken = after.ToString(CultureInfo.InvariantCulture);
        const string reasonCode = "v27-duration-preserving-first-candidate";
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = "items",
            DefinitionKind = "item",
            StableId = stableId,
            Metric = metric,
            Unit = "mEWU",
            Before = before.ToString(CultureInfo.InvariantCulture),
            After = afterToken,
            AuthoredRoundedValue = afterToken,
            PercentDelta = Token(percent),
            ExactFormula = metric == "acquisition-cost"
                ? "ceil(inputs+directWU+logistics+utility+loss / expectedOutput)"
                : "floor(acquisitionCost*salvageRetention)",
            BeforeBom = "N/A",
            AfterBom = "N/A",
            BeforeDirectWu = "N/A",
            AfterDirectWu = "N/A",
            BeforeBomEwu = "N/A",
            AfterBomEwu = "N/A",
            BeforeLaborDensity = "N/A",
            AfterLaborDensity = "N/A",
            UpstreamOnlyAfter = rootCauseIds.Length > 0
                ? afterToken
                : before.ToString(CultureInfo.InvariantCulture),
            InheritedDelta = rootCauseIds.Length > 0
                ? checked(after - before).ToString(CultureInfo.InvariantCulture)
                : "0",
            RawLocalDelta = rootCauseIds.Length > 0
                ? "0"
                : checked(after - before).ToString(CultureInfo.InvariantCulture),
            LocalQuantizationBoundaryCount = 2,
            DownstreamConsumerCount = downstream.ToString(CultureInfo.InvariantCulture),
            DependencyIds = dependencies,
            RootCauseIds = rootCauseIds,
            AnomalyDisposition = severity == BalanceAnomalySeverity.Critical
                ? DispositionToken(disposition)
                : severity == BalanceAnomalySeverity.Warning ? "warning" : "none",
            ReasonCode = reasonCode,
            ReasonDetail = "Audit-only; no ScriptableObject value was changed.",
            SourceAuthority = path,
            SourcePropertyPath = metric == "acquisition-cost" ? "recipe graph" : "salvageRetention",
            ExecutionRoute = selectedSourceId,
            SaveAuthority = "ScriptableObject catalog",
            VerificationEvidence = "V23-before|V27-fixed-point",
            ReviewStatus = severity == BalanceAnomalySeverity.Critical ? "pending" : "review",
            ApprovalKey = disposition == BalanceAnomalyDisposition.RootCritical
                    || disposition == BalanceAnomalyDisposition.LocalCritical
                ? BuildApprovalKey(stableId, metric, afterToken, dependencyFingerprint,
                    sourceDigest, reasonCode, ResolveBaselineRecordId(stableId))
                : string.Empty,
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = fingerprint,
            SourceDigest = sourceDigest,
            SemanticHash = HashText(definition.ItemId + "|" + metric + "|" + after),
            AssetApplied = "false",
            BalanceBaselineRecordId = ResolveBaselineRecordId(stableId)
        });
        if (severity != BalanceAnomalySeverity.None)
        {
            anomalies.Add(BalanceAnomalyNode.Capture(
                stableId,
                metric,
                severity,
                disposition,
                "v27-duration-preserving-first-candidate",
                rootCauseIds));
        }
    }

    private static void CaptureCropValues(
        IEnumerable<CropDefinitionSO> crops,
        IEnumerable<ItemDefinitionSO> items,
        EmbeddedWorkValueSnapshot before,
        V27EmbeddedWorkValueSnapshot after,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        Dictionary<string, ItemDefinitionSO> itemsById = items
            .Where(value => value != null)
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        if (!before.TryGetItemWork("resource:clean-water", out float beforeWaterWork)
            || !after.Items.TryGetValue("resource:clean-water", out V27ItemValue afterWater))
        {
            throw new InvalidOperationException(
                "Crop EWU requires both V23 and V27 clean-water authority.");
        }

        long beforeWaterMilli = V27EwuQuantizer.QuantizeInputDebit(
            BalanceCanonicalText.DecimalFromFiniteFloat(
                beforeWaterWork,
                "crop:before-clean-water")).MilliEwu;
        foreach (CropDefinitionSO crop in crops
                     .Where(value => value != null)
                     .OrderBy(value => value.CropId, StringComparer.Ordinal))
        {
            if (!after.Crops.TryGetValue(crop.CropId, out V27CropValueBreakdown full))
                throw new InvalidOperationException(
                    $"Crop EWU did not resolve: {crop.CropId}.");

            string cropId = RawStableId(crop, "cropId");
            decimal currentSow = BalanceCanonicalText.DecimalFromFiniteFloat(
                crop.SowWork,
                $"crop:{crop.CropId}:sowWork");
            decimal currentHarvest = BalanceCanonicalText.DecimalFromFiniteFloat(
                crop.HarvestWork,
                $"crop:{crop.CropId}:harvestWork");
            decimal beforeSow = ResolveCropAuthoredBefore(
                cropId,
                "sowWork",
                currentSow,
                historicalBeforeValues);
            decimal beforeHarvest = ResolveCropAuthoredBefore(
                cropId,
                "harvestWork",
                currentHarvest,
                historicalBeforeValues);
            CropCostCandidate beforeCandidate = CalculateCropCostCandidate(
                crop,
                itemsById,
                beforeWaterMilli,
                1m,
                beforeSow,
                beforeHarvest);
            CropCostCandidate upstreamOnly = CalculateCropCostCandidate(
                crop,
                itemsById,
                afterWater.AcquisitionCost.MilliEwu,
                1m,
                beforeSow,
                beforeHarvest);
            string path = AssetDatabase.GetAssetPath(crop);
            string sourceDigest = GetSourceDigest(path, sourceDigests);
            string[] dependencies = full.CleanWaterUnits > 0
                ? new[] { "resource:clean-water", crop.SeedItemId }
                : new[] { crop.SeedItemId };
            string dependencyFingerprint = HashText(string.Join("|", dependencies));
            string bom = crop.SeedItemId + "=1(catalyst;returned>=2)"
                + (full.CleanWaterUnits > 0
                    ? "|resource:clean-water=" + full.CleanWaterUnits.ToString(
                        CultureInfo.InvariantCulture)
                    : string.Empty);
            decimal beforeDirect = checked(beforeSow + beforeHarvest);
            decimal afterDirect = full.DirectWorkDebit.MilliEwu / 1000m;
            long beforePerUnit = beforeCandidate.PerUnitAcquisition.MilliEwu;
            long afterPerUnit = full.PerUnitAcquisition.MilliEwu;
            BalanceAnomalySeverity costSeverity = BalanceAnomalyDetector.ClassifyPercentDelta(
                Math.Abs(PercentDelta(beforePerUnit, afterPerUnit)));
            capture.Capture(new BalanceMetricCaptureRequest
            {
                Domain = "agriculture",
                DefinitionKind = "crop",
                StableId = cropId,
                Metric = "cultivated-acquisition-cost",
                Unit = "mEWU",
                Before = beforePerUnit.ToString(CultureInfo.InvariantCulture),
                After = afterPerUnit.ToString(CultureInfo.InvariantCulture),
                AuthoredRoundedValue = afterPerUnit.ToString(CultureInfo.InvariantCulture),
                PercentDelta = Token(PercentDelta(beforePerUnit, afterPerUnit)),
                ExactFormula = "ceil((water+ceil((sow+harvest)*laborScale)+"
                    + "ceil(logistics*laborScale)+ceil((direct*0.10+growthHours*0.25)*laborScale)"
                    + "+ceil(expectedLoss5%))/yield); seed is a returned non-market catalyst",
                BeforeBom = bom,
                AfterBom = bom,
                BeforeDirectWu = Token(beforeDirect),
                AfterDirectWu = Token(afterDirect),
                BeforeBomEwu = beforeCandidate.InputDebit.ToCanonicalEwuToken(),
                AfterBomEwu = full.InputDebit.ToCanonicalEwuToken(),
                BeforeLaborDensity = beforeCandidate.InputDebit.MilliEwu > 0L
                    ? Token(beforeDirect / (beforeCandidate.InputDebit.MilliEwu / 1000m))
                    : "N/A",
                AfterLaborDensity = full.InputDebit.MilliEwu > 0L
                    ? Token(afterDirect / (full.InputDebit.MilliEwu / 1000m))
                    : "N/A",
                UpstreamOnlyAfter = upstreamOnly.PerUnitAcquisition.MilliEwu.ToString(
                    CultureInfo.InvariantCulture),
                InheritedDelta = checked(
                    upstreamOnly.PerUnitAcquisition.MilliEwu - beforePerUnit).ToString(
                    CultureInfo.InvariantCulture),
                RawLocalDelta = checked(
                    afterPerUnit - upstreamOnly.PerUnitAcquisition.MilliEwu).ToString(
                    CultureInfo.InvariantCulture),
                LocalQuantizationBoundaryCount = 5,
                DownstreamConsumerCount = "1",
                DependencyIds = dependencies,
                RootCauseIds = costSeverity == BalanceAnomalySeverity.Critical
                    ? new[] { cropId }
                    : Array.Empty<string>(),
                AnomalyDisposition = costSeverity == BalanceAnomalySeverity.Critical
                    ? "local-critical"
                    : costSeverity == BalanceAnomalySeverity.Warning ? "warning" : "none",
                ReasonCode = "v27-cultivated-acquisition-authority",
                ReasonDetail = "The crop competes with external acquisition; physical seed input "
                    + "is preserved because every lawful harvest returns at least two seed lots.",
                SourceAuthority = path,
                SourcePropertyPath = "growthHours|sowWork|harvestWork|dailyWater|yield|seedItemId",
                ExecutionRoute = "CropDefinitionSO->CropPlotRuntime.Sow/Harvest->physical output",
                SaveAuthority = "CropDefinitionSO + CropPlotSaveData + CropEcologyWorldSaveData",
                VerificationEvidence = "V27 crop fixed-point audit; PlayMode evidence pending",
                ReviewStatus = costSeverity == BalanceAnomalySeverity.Critical ? "pending" : "review",
                ApprovalKey = string.Empty,
                DependencyFingerprint = dependencyFingerprint,
                LocalFingerprint = HashText(
                    cropId + "|" + bom + "|" + crop.GrowthHours.ToString("R", CultureInfo.InvariantCulture)
                    + "|" + Token(beforeSow)
                    + "|" + Token(beforeHarvest)
                    + "|" + crop.Yield.ToString(CultureInfo.InvariantCulture)),
                SourceDigest = sourceDigest,
                SemanticHash = HashText(cropId + "|cultivated-acquisition-cost|" + afterPerUnit),
                AssetApplied = "false",
                BalanceBaselineRecordId = ResolveBaselineRecordId(cropId)
            });
            if (costSeverity != BalanceAnomalySeverity.None)
            {
                anomalies.Add(BalanceAnomalyNode.Capture(
                    cropId,
                    "cultivated-acquisition-cost",
                    costSeverity,
                    costSeverity == BalanceAnomalySeverity.Critical
                        ? BalanceAnomalyDisposition.LocalCritical
                        : BalanceAnomalyDisposition.None,
                    "v27-cultivated-acquisition-authority",
                    dependencies));
            }

            CaptureCropAuthoredWorkMetric(
                crop,
                cropId,
                "authored-sow-wu",
                "sowWork",
                crop.SowWork,
                path,
                sourceDigest,
                dependencies,
                dependencyFingerprint,
                bom,
                capture,
                anomalies,
                historicalBeforeValues);
            CaptureCropAuthoredWorkMetric(
                crop,
                cropId,
                "authored-harvest-wu",
                "harvestWork",
                crop.HarvestWork,
                path,
                sourceDigest,
                dependencies,
                dependencyFingerprint,
                bom,
                capture,
                anomalies,
                historicalBeforeValues);
        }
    }

    private static IReadOnlyDictionary<string, long> BuildRouteComparableBeforeItemValues(
        IEnumerable<CropDefinitionSO> crops,
        IEnumerable<ItemDefinitionSO> items,
        EmbeddedWorkValueSnapshot before,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        Dictionary<string, ItemDefinitionSO> itemsById = items
            .Where(value => value != null)
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        Dictionary<string, long> values = before.ItemWork
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToDictionary(
                value => value.Key,
                value => V27EwuQuantizer.QuantizeInputDebit(
                    BalanceCanonicalText.DecimalFromFiniteFloat(
                        value.Value,
                        $"route-comparable-before:{value.Key}")).MilliEwu,
                StringComparer.Ordinal);
        if (!values.TryGetValue("resource:clean-water", out long beforeWater))
        {
            throw new InvalidOperationException(
                "Route-comparable crop valuation requires V23 clean-water authority.");
        }
        foreach (CropDefinitionSO crop in crops
                     .Where(value => value != null)
                     .OrderBy(value => value.CropId, StringComparer.Ordinal))
        {
            decimal currentSow = BalanceCanonicalText.DecimalFromFiniteFloat(
                crop.SowWork,
                $"crop:{crop.CropId}:sowWork");
            decimal currentHarvest = BalanceCanonicalText.DecimalFromFiniteFloat(
                crop.HarvestWork,
                $"crop:{crop.CropId}:harvestWork");
            CropCostCandidate candidate = CalculateCropCostCandidate(
                crop,
                itemsById,
                beforeWater,
                1m,
                ResolveCropAuthoredBefore(
                    crop.CropId,
                    "sowWork",
                    currentSow,
                    historicalBeforeValues),
                ResolveCropAuthoredBefore(
                    crop.CropId,
                    "harvestWork",
                    currentHarvest,
                    historicalBeforeValues));
            if (!values.TryGetValue(crop.HarvestItemId, out long current)
                || candidate.PerUnitAcquisition.MilliEwu < current)
            {
                values[crop.HarvestItemId] = candidate.PerUnitAcquisition.MilliEwu;
            }
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, long>(values);
    }

    private static void CaptureCropAuthoredWorkMetric(
        CropDefinitionSO crop,
        string cropId,
        string metric,
        string propertyPath,
        float beforeWork,
        string path,
        string sourceDigest,
        string[] dependencies,
        string dependencyFingerprint,
        string bom,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        decimal current = BalanceCanonicalText.DecimalFromFiniteFloat(
            beforeWork,
            $"crop:{cropId}:{propertyPath}");
        decimal before = ResolveCropAuthoredBefore(
            cropId,
            propertyPath,
            current,
            historicalBeforeValues);
        decimal legacyScaled = decimal.Ceiling(before * LaborScale);
        decimal after = before;
        if (current != legacyScaled && current != after)
        {
            throw new InvalidOperationException(
                $"Crop authored work drifted outside its recurring-throughput correction: "
                + $"{cropId}:{propertyPath}; current={Token(current)}, "
                + $"legacy={Token(legacyScaled)}, after={Token(after)}.");
        }
        string afterToken = Token(after);
        string approvalSourceDigest = GetApprovalSourceDigest(
            path,
            "sowWork",
            "harvestWork");
        BalanceAnomalySeverity severity = BalanceAnomalyDetector.ClassifyPercentDelta(
            Math.Abs(PercentDelta(legacyScaled, after)));
        const string reasonCode = "v27-recurring-throughput-no-project-scale";
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = "agriculture",
            DefinitionKind = "crop",
            StableId = cropId,
            Metric = metric,
            Unit = "WU",
            Before = Token(legacyScaled),
            After = afterToken,
            AuthoredRoundedValue = afterToken,
            PercentDelta = Token(PercentDelta(legacyScaled, after)),
            ExactFormula = "recurring crop WU = frozen V23 authored cycle WU; no 2.25 project scale",
            BeforeBom = bom,
            AfterBom = bom,
            BeforeDirectWu = Token(legacyScaled),
            AfterDirectWu = afterToken,
            BeforeBomEwu = "see:cultivated-acquisition-cost",
            AfterBomEwu = "see:cultivated-acquisition-cost",
            BeforeLaborDensity = "see:cultivated-acquisition-cost",
            AfterLaborDensity = "see:cultivated-acquisition-cost",
            UpstreamOnlyAfter = Token(legacyScaled),
            InheritedDelta = "0",
            RawLocalDelta = Token(after - legacyScaled),
            LocalQuantizationBoundaryCount = 1,
            DownstreamConsumerCount = "1",
            DependencyIds = dependencies,
            RootCauseIds = Array.Empty<string>(),
            AnomalyDisposition = severity == BalanceAnomalySeverity.Critical
                ? "local-critical"
                : severity == BalanceAnomalySeverity.Warning ? "warning" : "none",
            ReasonCode = reasonCode,
            ReasonDetail = "Corrects a recurring crop cycle that was incorrectly scaled as a one-shot project.",
            SourceAuthority = path,
            SourcePropertyPath = propertyPath,
            ExecutionRoute = "CropDefinitionSO->CropPlotRuntime->AIWork",
            SaveAuthority = "CropDefinitionSO",
            VerificationEvidence = "V27 crop audit; PlayMode evidence pending",
            ReviewStatus = "pending",
            ApprovalKey = legacyScaled != after
                ? BuildApprovalKey(
                    cropId,
                    metric,
                    afterToken,
                    dependencyFingerprint,
                    approvalSourceDigest,
                    reasonCode,
                    ResolveLaborBaselineRecordId(cropId))
                : string.Empty,
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = HashText(
                cropId + "|" + metric + "|" + Token(before) + "|" + crop.Yield),
            SourceDigest = approvalSourceDigest,
            SemanticHash = HashText(cropId + "|" + metric + "|" + afterToken),
            AssetApplied = current == after ? "true" : "false",
            BalanceBaselineRecordId = ResolveLaborBaselineRecordId(cropId)
        });
        if (severity != BalanceAnomalySeverity.None)
        {
            anomalies.Add(BalanceAnomalyNode.Capture(
                cropId,
                metric,
                severity,
                severity == BalanceAnomalySeverity.Critical
                    ? BalanceAnomalyDisposition.LocalCritical
                    : BalanceAnomalyDisposition.None,
                reasonCode,
                Array.Empty<string>()));
        }
    }

    private static CropCostCandidate CalculateCropCostCandidate(
        CropDefinitionSO crop,
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        long cleanWaterMilliEwu,
        decimal laborScale,
        decimal sowWork,
        decimal harvestWork)
    {
        decimal growthHours = BalanceCanonicalText.DecimalFromFiniteFloat(
            crop.GrowthHours,
            $"crop:{crop.CropId}:growthHours");
        decimal dailyWater = BalanceCanonicalText.DecimalFromFiniteFloat(
            crop.DailyWater,
            $"crop:{crop.CropId}:dailyWater");
        int waterUnits = dailyWater <= 0m
            ? 0
            : checked((int)decimal.Ceiling(dailyWater * growthHours / 24m));
        EwuAmount input = EwuAmount.FromMilliEwu(
            checked(cleanWaterMilliEwu * waterUnits));
        decimal directBefore = checked(sowWork + harvestWork);
        EwuAmount direct = V27EwuQuantizer.QuantizeInputDebit(
            checked(directBefore * laborScale));
        decimal inputWeight = ResolveCapturedItemWeight(items, crop.SeedItemId)
            + waterUnits * ResolveCapturedItemWeight(items, "resource:clean-water");
        decimal outputWeight = checked(
            crop.Yield * ResolveCapturedItemWeight(items, crop.HarvestItemId));
        decimal logisticsBefore = checked(
            3m
            + (waterUnits > 0 ? 2m : 1m) * 0.75m
            + 2m * 0.50m
            + DecimalSquareRootForAudit(inputWeight + outputWeight) * 0.60m);
        EwuAmount logistics = V27EwuQuantizer.QuantizeInputDebit(
            checked(logisticsBefore * laborScale));
        EwuAmount infrastructure = V27EwuQuantizer.QuantizeInputDebit(
            checked((directBefore * 0.10m + growthHours * 0.25m) * laborScale));
        EwuAmount subtotal = input + direct + logistics + infrastructure;
        EwuAmount loss = V27EwuQuantizer.MultiplyInputDebit(subtotal, 0.05m);
        EwuAmount total = subtotal + loss;
        return new CropCostCandidate(
            input,
            direct,
            logistics,
            infrastructure,
            loss,
            V27EwuQuantizer.DivideInputCost(total.MilliEwu, crop.Yield));
    }

    private static decimal ResolveCapturedItemWeight(
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        string itemId)
    {
        if (!items.TryGetValue(itemId, out ItemDefinitionSO item))
            throw new InvalidOperationException($"Crop item weight authority is missing: {itemId}.");
        return BalanceCanonicalText.DecimalFromFiniteFloat(
            item.UnitWeight,
            $"crop:item-weight:{itemId}");
    }

    private static decimal DecimalSquareRootForAudit(decimal value)
    {
        if (value < 0m)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (value == 0m)
            return 0m;
        decimal estimate = value >= 1m ? value : 1m;
        for (int iteration = 0; iteration < 32; iteration++)
            estimate = (estimate + value / estimate) / 2m;
        return estimate;
    }

    private static void CaptureRecipeValues(
        IEnumerable<ProductionRecipeSO> recipes,
        EmbeddedWorkValueSnapshot before,
        V27EmbeddedWorkValueSnapshot after,
        IReadOnlyDictionary<string, long> routeComparableBeforeItemValues,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        ProductionRecipeSO[] canonicalRecipes = recipes
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, ProductionRecipeSO> recipesById = canonicalRecipes
            .ToDictionary(value => value.RecipeId, StringComparer.Ordinal);
        foreach (ProductionRecipeSO recipe in canonicalRecipes)
        {
            if (!before.Recipes.TryGetValue(recipe.RecipeId, out EmbeddedWorkValueRecipeBreakdown beforeValue)
                || !after.Recipes.TryGetValue(recipe.RecipeId, out V27RecipeValueBreakdown afterValue))
            {
                if (recipe.Outputs.Count == 0)
                {
                    CaptureTerminalSinkRecipe(
                        recipe,
                        routeComparableBeforeItemValues,
                        after,
                        capture,
                        sourceDigests);
                }
                continue;
            }
            string path = AssetDatabase.GetAssetPath(recipe);
            string sourceDigest = GetSourceDigest(path, sourceDigests);
            string approvalSourceDigest = GetRecipeWorkApprovalSourceDigest(
                recipe,
                sourceDigests);
            string stableId = RawStableId(recipe, "recipeId");
            decimal v23DirectWu = BalanceCanonicalText.DecimalFromFiniteFloat(
                beforeValue.DirectWork,
                $"recipe:{recipe.RecipeId}:beforeDirectWU");
            decimal historicalAuthoredWu = ResolveHistoricalAuthoredBefore(
                stableId,
                "authored-required-wu",
                v23DirectWu,
                historicalBeforeValues);
            decimal beforeWu = decimal.Ceiling(historicalAuthoredWu * LaborScale);
            decimal afterWu = afterValue.DirectWorkDebit.MilliEwu / 1000m;
            long comparableBeforeInputMilli = 0L;
            foreach (ItemAmountDefinition input in recipe.Inputs)
            {
                if (!routeComparableBeforeItemValues.TryGetValue(
                        input.ItemId,
                        out long unitBefore))
                {
                    throw new InvalidOperationException(
                        $"Route-comparable Before value is missing: {input.ItemId}.");
                }
                comparableBeforeInputMilli = checked(
                    comparableBeforeInputMilli + unitBefore * input.Amount);
            }
            decimal comparableBeforeInput = comparableBeforeInputMilli / 1000m;
            decimal percent = PercentDelta(beforeWu, afterWu);
            BalanceAnomalySeverity percentSeverity = BalanceAnomalyDetector.ClassifyPercentDelta(
                Math.Abs(percent));
            // A recurring-throughput density comparison must use the frozen
            // per-batch work on both sides. Using the accidentally project-scaled
            // legacy display value here manufactures a 1/2.25 density collapse
            // even when the recipe and its input economics are unchanged.
            decimal beforeDensity = comparableBeforeInput > 0m
                ? historicalAuthoredWu / comparableBeforeInput
                : 0m;
            decimal afterDensity = afterValue.InputDebit.MilliEwu > 0L
                ? afterWu / (afterValue.InputDebit.MilliEwu / 1000m)
                : 0m;
            decimal densityRatio = beforeDensity > 0m && afterDensity > 0m
                ? afterDensity / beforeDensity
                : 1m;
            BalanceAnomalySeverity densitySeverity =
                BalanceAnomalyDetector.ClassifyLaborDensity(densityRatio);
            BalanceAnomalySeverity directWorkSeverity = percentSeverity;
            string bom = FormatBom(recipe.Inputs);
            string[] dependencies = recipe.Inputs.Select(value => value.ItemId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string localFingerprint = HashText(
                recipe.RecipeId + "|" + bom + "|" + recipe.Outputs.Count + "|"
                + recipe.RequiredWork.ToString("R", CultureInfo.InvariantCulture));
            string dependencyFingerprint = HashText(string.Join("|", dependencies));
            string[] densityRootCauseIds = ResolveRecipeDensityRootCauseIds(
                recipe,
                routeComparableBeforeItemValues,
                after,
                recipesById);
            BalanceAnomalyDisposition densityDisposition =
                densitySeverity != BalanceAnomalySeverity.Critical
                    ? BalanceAnomalyDisposition.None
                    : densityRootCauseIds.Length == 0
                        ? BalanceAnomalyDisposition.RootCritical
                        : densityRootCauseIds.Length == 1
                            ? BalanceAnomalyDisposition.CollapsedInheritedOnly
                            : BalanceAnomalyDisposition.CollapsedMultiRoot;
            string afterToken = Token(afterWu);
            const string reasonCode = "v27-recurring-throughput-no-project-scale";
            decimal authoredCurrent = BalanceCanonicalText.DecimalFromFiniteFloat(
                recipe.RequiredWork,
                $"recipe:{recipe.RecipeId}:requiredWork");
            capture.Capture(new BalanceMetricCaptureRequest
            {
                Domain = "production",
                DefinitionKind = "recipe",
                StableId = stableId,
                Metric = "direct-wu",
                Unit = "WU",
                Before = Token(beforeWu),
                After = afterToken,
                AuthoredRoundedValue = afterToken,
                PercentDelta = Token(percent),
                ExactFormula = "recurring recipe direct WU = frozen V23 batch WU; no 2.25 project scale",
                BeforeBom = bom,
                AfterBom = bom,
                BeforeDirectWu = Token(beforeWu),
                AfterDirectWu = Token(afterWu),
                BeforeBomEwu = Token(comparableBeforeInput),
                AfterBomEwu = afterValue.InputDebit.ToCanonicalEwuToken(),
                BeforeLaborDensity = comparableBeforeInput > 0m
                    ? Token(beforeDensity)
                    : "N/A",
                AfterLaborDensity = afterValue.InputDebit.MilliEwu > 0L
                    ? Token(afterDensity)
                    : "N/A",
                UpstreamOnlyAfter = Token(beforeWu),
                InheritedDelta = "0",
                RawLocalDelta = Token(afterWu - beforeWu),
                LocalQuantizationBoundaryCount = recipe.Inputs.Count + 4,
                DownstreamConsumerCount = recipe.Outputs.Count.ToString(CultureInfo.InvariantCulture),
                DependencyIds = dependencies,
                RootCauseIds = Array.Empty<string>(),
                AnomalyDisposition = directWorkSeverity == BalanceAnomalySeverity.Critical
                    ? "root-critical"
                    : directWorkSeverity == BalanceAnomalySeverity.Warning ? "warning" : "none",
                ReasonCode = reasonCode,
                ReasonDetail = "Corrects a recurring batch that was incorrectly scaled as a one-shot project; density review is emitted as a separate upstream-attributed metric; same-route labor-density ratio="
                    + Token(densityRatio)
                    + "; crop inputs use cultivated Before acquisition while V23 item rows remain frozen.",
                SourceAuthority = path,
                SourcePropertyPath = "derived:V23BalanceWorkCalculator.CalculateRecipe",
                ExecutionRoute = "ProductionRecipeSO->ProductionBillRuntime->AIWork",
                SaveAuthority = "ProductionRecipeSO",
                VerificationEvidence = "V27 recipe graph audit",
                ReviewStatus = directWorkSeverity == BalanceAnomalySeverity.Critical ? "pending" : "review",
                ApprovalKey = beforeWu != afterWu
                    ? BuildApprovalKey(
                        stableId,
                        "direct-wu",
                        afterToken,
                        dependencyFingerprint,
                        approvalSourceDigest,
                        reasonCode,
                        ResolveLaborBaselineRecordId(stableId))
                    : string.Empty,
                DependencyFingerprint = dependencyFingerprint,
                LocalFingerprint = localFingerprint,
                SourceDigest = approvalSourceDigest,
                SemanticHash = HashText(recipe.RecipeId + "|direct-wu|" + Token(afterWu)),
                AssetApplied = authoredCurrent == afterWu ? "true" : "false",
                BalanceBaselineRecordId = ResolveLaborBaselineRecordId(stableId)
            });
            CaptureRecipeLaborDensityMetric(
                recipe,
                stableId,
                path,
                approvalSourceDigest,
                dependencies,
                dependencyFingerprint,
                bom,
                beforeDensity,
                afterDensity,
                densityRatio,
                densitySeverity,
                densityDisposition,
                densityRootCauseIds,
                capture,
                anomalies);
            decimal authoredBefore = ResolveHistoricalAuthoredBefore(
                stableId,
                "authored-required-wu",
                authoredCurrent,
                historicalBeforeValues);
            decimal legacyAuthored = decimal.Ceiling(authoredBefore * LaborScale);
            decimal authoredAfter = authoredBefore;
            if (authoredCurrent != legacyAuthored && authoredCurrent != authoredAfter)
            {
                throw new InvalidOperationException(
                    $"Recipe authored work drifted outside its recurring-throughput correction: "
                    + $"{recipe.RecipeId}; current={Token(authoredCurrent)}, "
                    + $"legacy={Token(legacyAuthored)}, after={Token(authoredAfter)}.");
            }
            string authoredAfterToken = Token(authoredAfter);
            string authoredFingerprint = HashText(
                recipe.RecipeId + "|requiredWork|" + Token(authoredBefore));
            capture.Capture(new BalanceMetricCaptureRequest
            {
                Domain = "production",
                DefinitionKind = "recipe",
                StableId = stableId,
                Metric = "authored-required-wu",
                Unit = "WU",
                Before = Token(legacyAuthored),
                After = authoredAfterToken,
                AuthoredRoundedValue = authoredAfterToken,
                PercentDelta = Token(PercentDelta(legacyAuthored, authoredAfter)),
                ExactFormula = "recurring authored batch WU = frozen V23 WU; no 2.25 project scale",
                BeforeBom = bom,
                AfterBom = bom,
                BeforeDirectWu = Token(legacyAuthored),
                AfterDirectWu = authoredAfterToken,
                BeforeBomEwu = Token(comparableBeforeInput),
                AfterBomEwu = afterValue.InputDebit.ToCanonicalEwuToken(),
                BeforeLaborDensity = comparableBeforeInput > 0m
                    ? Token(authoredBefore / comparableBeforeInput)
                    : "N/A",
                AfterLaborDensity = afterValue.InputDebit.MilliEwu > 0L
                    ? Token(authoredAfter / (afterValue.InputDebit.MilliEwu / 1000m))
                    : "N/A",
                UpstreamOnlyAfter = Token(legacyAuthored),
                InheritedDelta = "0",
                RawLocalDelta = Token(authoredAfter - legacyAuthored),
                LocalQuantizationBoundaryCount = 1,
                DownstreamConsumerCount = recipe.Outputs.Count.ToString(CultureInfo.InvariantCulture),
                DependencyIds = dependencies,
                RootCauseIds = Array.Empty<string>(),
                AnomalyDisposition = "warning",
                ReasonCode = reasonCode,
                ReasonDetail = "Recurring-throughput correction; runtime work authority is the authored batch WU through "
                    + "V27BalanceWorkCalculator. Same-route labor-density ratio="
                    + Token(densityRatio) + "; explicit exact approval required.",
                SourceAuthority = path,
                SourcePropertyPath = "requiredWork",
                ExecutionRoute = "ProductionRecipeSO authored display + V27BalanceWorkCalculator->ProductionBillRuntime->AIWork",
                SaveAuthority = "ProductionRecipeSO authored display + V27 runtime formula",
                VerificationEvidence = "V27 authored work audit",
                ReviewStatus = "pending",
                ApprovalKey = legacyAuthored != authoredAfter
                    ? BuildApprovalKey(stableId, "authored-required-wu", authoredAfterToken,
                        dependencyFingerprint, approvalSourceDigest, reasonCode,
                        ResolveLaborBaselineRecordId(stableId))
                    : string.Empty,
                DependencyFingerprint = dependencyFingerprint,
                LocalFingerprint = authoredFingerprint,
                SourceDigest = approvalSourceDigest,
                SemanticHash = HashText(
                    recipe.RecipeId + "|authored-required-wu|" + authoredAfterToken),
                AssetApplied = authoredCurrent == authoredAfter ? "true" : "false",
                BalanceBaselineRecordId = ResolveLaborBaselineRecordId(stableId)
            });
            if (directWorkSeverity != BalanceAnomalySeverity.None)
            {
                anomalies.Add(BalanceAnomalyNode.Capture(
                    recipe.RecipeId,
                    "direct-wu",
                    directWorkSeverity,
                    directWorkSeverity == BalanceAnomalySeverity.Critical
                        ? BalanceAnomalyDisposition.RootCritical
                        : BalanceAnomalyDisposition.None,
                    reasonCode,
                    Array.Empty<string>()));
            }
        }
    }

    private static void CaptureTerminalSinkRecipe(
        ProductionRecipeSO recipe,
        IReadOnlyDictionary<string, long> routeComparableBeforeItemValues,
        V27EmbeddedWorkValueSnapshot after,
        BalanceCaptureFactory capture,
        IDictionary<string, string> sourceDigests)
    {
        string path = AssetDatabase.GetAssetPath(recipe);
        string sourceDigest = GetSourceDigest(path, sourceDigests);
        string stableId = RawStableId(recipe, "recipeId");
        string bom = FormatBom(recipe.Inputs);
        string[] dependencies = recipe.Inputs
            .Select(value => value.ItemId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        long beforeInputMilli = 0L;
        long afterInputMilli = 0L;
        foreach (ItemAmountDefinition input in recipe.Inputs)
        {
            if (!routeComparableBeforeItemValues.TryGetValue(
                    input.ItemId,
                    out long beforeUnit)
                || !after.Items.TryGetValue(
                    input.ItemId,
                    out V27ItemValue afterValue))
            {
                throw new InvalidOperationException(
                    "Terminal sink recipe input value is missing: "
                    + recipe.RecipeId + ":" + input.ItemId);
            }
            beforeInputMilli = checked(
                beforeInputMilli + beforeUnit * input.Amount);
            afterInputMilli = checked(
                afterInputMilli
                + afterValue.AcquisitionCost.MilliEwu * input.Amount);
        }

        decimal authoredWu = BalanceCanonicalText.DecimalFromFiniteFloat(
            recipe.RequiredWork,
            $"recipe:{recipe.RecipeId}:terminalSinkRequiredWork");
        string wuToken = Token(authoredWu);
        string dependencyFingerprint = HashText(string.Join("|", dependencies));
        const string reasonCode = "v27-typed-terminal-sink-census";
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = "production",
            DefinitionKind = "recipe",
            StableId = stableId,
            Metric = "direct-wu",
            Unit = "WU",
            Before = wuToken,
            After = wuToken,
            AuthoredRoundedValue = wuToken,
            PercentDelta = "0",
            ExactFormula = "terminal sink: all physical inputs become typed process loss",
            BeforeBom = bom,
            AfterBom = bom,
            BeforeDirectWu = wuToken,
            AfterDirectWu = wuToken,
            BeforeBomEwu = Token(beforeInputMilli / 1000m),
            AfterBomEwu = Token(afterInputMilli / 1000m),
            BeforeLaborDensity = "N/A",
            AfterLaborDensity = "N/A",
            UpstreamOnlyAfter = wuToken,
            InheritedDelta = "0",
            RawLocalDelta = "0",
            LocalQuantizationBoundaryCount = recipe.Inputs.Count + 2,
            DownstreamConsumerCount = "0",
            DependencyIds = dependencies,
            RootCauseIds = Array.Empty<string>(),
            AnomalyDisposition = "none",
            ReasonCode = reasonCode,
            ReasonDetail = "Zero-output terminal Sink is excluded from value relaxation and SCC transforms but remains present in the exhaustive recipe ledger.",
            SourceAuthority = path,
            SourcePropertyPath = "requiredWork",
            ExecutionRoute = "ProductionRecipeSO->ProductionBillRuntime->typed terminal Sink",
            SaveAuthority = "ProductionRecipeSO",
            VerificationEvidence = "V27 terminal sink recipe census",
            ReviewStatus = "verified",
            ApprovalKey = string.Empty,
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = HashText(
                recipe.RecipeId + "|terminal-sink|" + bom + "|" + wuToken),
            SourceDigest = sourceDigest,
            SemanticHash = HashText(
                recipe.RecipeId + "|terminal-sink|" + afterInputMilli),
            AssetApplied = "true",
            BalanceBaselineRecordId = ResolveLaborBaselineRecordId(stableId)
        });
    }

    internal static string[] ResolveItemMetricRootCauseIds(
        string stableId,
        string metric,
        string selectedSourceId,
        ISet<string> semanticRootIds,
        bool acquisitionEmitsRootCritical)
    {
        if (string.IsNullOrEmpty(stableId))
            throw new ArgumentException("Item stable ID is required.", nameof(stableId));
        if (string.IsNullOrEmpty(metric))
            throw new ArgumentException("Item metric is required.", nameof(metric));
        if (string.IsNullOrEmpty(selectedSourceId))
            throw new ArgumentException("Selected item source is required.", nameof(selectedSourceId));
        if (semanticRootIds == null)
            throw new ArgumentNullException(nameof(semanticRootIds));

        if (semanticRootIds.Contains(selectedSourceId))
            return new[] { selectedSourceId };
        if (string.Equals(metric, "recoverable-value", StringComparison.Ordinal)
            && acquisitionEmitsRootCritical)
        {
            return new[] { stableId };
        }
        return Array.Empty<string>();
    }

    private static BalanceAnomalySeverity ClassifyItemMetricSeverity(
        ItemDefinitionSO definition,
        long before,
        long after,
        int downstream)
    {
        decimal absolutePercent = Math.Abs(PercentDelta(before, after));
        BalanceAnomalySeverity severity =
            BalanceAnomalyDetector.ClassifyPercentDelta(absolutePercent);
        if (definition is ResourceItemDefinitionSO resource
            && resource.Kind == ResourceItemKind.Raw)
        {
            severity = Max(
                severity,
                BalanceAnomalyDetector.ClassifyPrimitiveDelta(
                    absolutePercent,
                    downstream));
        }
        return severity;
    }

    private static string GetRecipeWorkApprovalSourceDigest(
        ProductionRecipeSO recipe,
        IDictionary<string, string> sourceDigests)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        const string WorkCalculatorPath =
            "Assets/Scripts/Services/Economy/V27BalanceWorkCalculator.cs";
        string calculatorDigest = GetSourceDigest(
            WorkCalculatorPath,
            sourceDigests);
        return HashText(
            "recipe-direct-wu-approval-subject@1|"
            + recipe.RecipeId + "|"
            + recipe.ProcessKind + "|"
            + calculatorDigest);
    }

    private static string[] ResolveRecipeDensityRootCauseIds(
        ProductionRecipeSO recipe,
        IReadOnlyDictionary<string, long> beforeItemValues,
        V27EmbeddedWorkValueSnapshot after,
        IReadOnlyDictionary<string, ProductionRecipeSO> recipesById)
    {
        SortedSet<string> roots = new SortedSet<string>(StringComparer.Ordinal);
        HashSet<string> visitingItems = new HashSet<string>(StringComparer.Ordinal);
        foreach (ItemAmountDefinition input in recipe.Inputs)
        {
            CollectItemEconomicRootCauses(
                input.ItemId,
                beforeItemValues,
                after,
                recipesById,
                visitingItems,
                roots);
        }
        return roots.ToArray();
    }

    private static bool CollectItemEconomicRootCauses(
        string itemId,
        IReadOnlyDictionary<string, long> beforeItemValues,
        V27EmbeddedWorkValueSnapshot after,
        IReadOnlyDictionary<string, ProductionRecipeSO> recipesById,
        ISet<string> visitingItems,
        ISet<string> roots)
    {
        if (!beforeItemValues.TryGetValue(itemId, out long beforeValue)
            || !after.Items.TryGetValue(itemId, out V27ItemValue afterValue))
        {
            throw new InvalidOperationException(
                $"Recipe density attribution cannot resolve item '{itemId}'.");
        }
        if (beforeValue == afterValue.AcquisitionCost.MilliEwu)
            return false;
        if (!visitingItems.Add(itemId))
        {
            roots.Add(itemId);
            return true;
        }

        try
        {
            string sourceId = afterValue.SelectedSourceId;
            if (sourceId.StartsWith("external:", StringComparison.Ordinal))
            {
                // External acquisition has no separate authored producer row;
                // the item acquisition row is the approvable root authority.
                roots.Add(itemId);
                return true;
            }
            if (!sourceId.StartsWith("recipe:", StringComparison.Ordinal)
                || !recipesById.TryGetValue(sourceId, out ProductionRecipeSO sourceRecipe))
            {
                roots.Add(sourceId);
                return true;
            }

            bool inheritedRootFound = false;
            foreach (ItemAmountDefinition sourceInput in sourceRecipe.Inputs)
            {
                inheritedRootFound |= CollectItemEconomicRootCauses(
                    sourceInput.ItemId,
                    beforeItemValues,
                    after,
                    recipesById,
                    visitingItems,
                    roots);
            }
            if (!inheritedRootFound)
            {
                // The selected producer changed without an inherited input-cost
                // change (for example a work formula or output allocation
                // revision), so that producer is the actual review root.
                roots.Add(sourceId);
            }
            return true;
        }
        finally
        {
            visitingItems.Remove(itemId);
        }
    }

    private static void CaptureRecipeLaborDensityMetric(
        ProductionRecipeSO recipe,
        string stableId,
        string path,
        string sourceDigest,
        string[] dependencies,
        string dependencyFingerprint,
        string bom,
        decimal beforeDensity,
        decimal afterDensity,
        decimal densityRatio,
        BalanceAnomalySeverity severity,
        BalanceAnomalyDisposition disposition,
        string[] rootCauseIds,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies)
    {
        const string Metric = "labor-density-ratio";
        const string ReasonCode = "labor-density-drift";
        decimal directWu = BalanceCanonicalText.DecimalFromFiniteFloat(
            recipe.RequiredWork,
            $"recipe:{recipe.RecipeId}:density-direct-wu");
        string beforeToken = Token(beforeDensity);
        string afterToken = Token(afterDensity);
        string[] roots = severity == BalanceAnomalySeverity.Critical
            ? rootCauseIds
            : Array.Empty<string>();
        bool emitsApproval = severity == BalanceAnomalySeverity.Critical
            && (disposition == BalanceAnomalyDisposition.RootCritical
                || disposition == BalanceAnomalyDisposition.LocalCritical);
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = "production",
            DefinitionKind = "recipe",
            StableId = stableId,
            Metric = Metric,
            Unit = "direct-WU/BOM-EWU",
            Before = beforeToken,
            After = afterToken,
            AuthoredRoundedValue = afterToken,
            PercentDelta = Token(PercentDelta(beforeDensity, afterDensity)),
            ExactFormula = "same-route recurring direct WU / input BOM acquisition EWU; project-scale correction excluded from both sides",
            BeforeBom = bom,
            AfterBom = bom,
            BeforeDirectWu = Token(directWu),
            AfterDirectWu = Token(directWu),
            BeforeBomEwu = beforeDensity > 0m
                ? Token(directWu / beforeDensity)
                : "N/A",
            AfterBomEwu = afterDensity > 0m
                ? Token(directWu / afterDensity)
                : "N/A",
            BeforeLaborDensity = beforeToken,
            AfterLaborDensity = afterToken,
            UpstreamOnlyAfter = afterToken,
            InheritedDelta = Token(afterDensity - beforeDensity),
            RawLocalDelta = "0",
            LocalQuantizationBoundaryCount = recipe.Inputs.Count + 4,
            DownstreamConsumerCount = recipe.Outputs.Count.ToString(
                CultureInfo.InvariantCulture),
            DependencyIds = dependencies,
            RootCauseIds = roots,
            AnomalyDisposition = severity == BalanceAnomalySeverity.Critical
                ? DispositionToken(disposition)
                : severity == BalanceAnomalySeverity.Warning ? "warning" : "none",
            ReasonCode = ReasonCode,
            ReasonDetail = "Derived density-only review. The recipe's local authored work is unchanged; input acquisition changes are attributed recursively to their selected producer roots.",
            SourceAuthority = path,
            SourcePropertyPath = "derived:requiredWork/input-acquisition-cost",
            ExecutionRoute = "V27 recipe graph density attribution",
            SaveAuthority = "derived ledger metric",
            VerificationEvidence = "V27 same-route density audit",
            ReviewStatus = severity == BalanceAnomalySeverity.Critical
                ? "pending"
                : "review",
            ApprovalKey = emitsApproval
                ? BuildApprovalKey(
                    stableId,
                    Metric,
                    afterToken,
                    dependencyFingerprint,
                    sourceDigest,
                    ReasonCode,
                    ResolveLaborBaselineRecordId(stableId))
                : string.Empty,
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = HashText(
                stableId + "|" + Metric + "|" + bom + "|"
                + Token(directWu)),
            SourceDigest = sourceDigest,
            SemanticHash = HashText(stableId + "|" + Metric + "|" + afterToken),
            AssetApplied = "true",
            BalanceBaselineRecordId = ResolveLaborBaselineRecordId(stableId)
        });
        if (severity != BalanceAnomalySeverity.None)
        {
            anomalies.Add(BalanceAnomalyNode.Capture(
                recipe.RecipeId,
                Metric,
                severity,
                disposition,
                ReasonCode,
                roots));
        }
    }

    private static IReadOnlyDictionary<string, V27ConstructionRedistributionResult>
        CaptureBuildingCandidates(
        IEnumerable<BuildingSO> definitions,
        EmbeddedWorkValueSnapshot beforeValues,
        V27EmbeddedWorkValueSnapshot afterValues,
        V23BalanceWorkCalculator work,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests,
        IReadOnlyDictionary<string, string> historicalBeforeValues,
        IReadOnlyDictionary<string, string> previouslyApprovedAfterValues)
    {
        Dictionary<string, V27ConstructionRedistributionResult> results = new(
            StringComparer.Ordinal);
        foreach (BuildingSO building in definitions
                     .Where(value => value != null && value.id >= 0 && !value.IsDeprecatedCompatibilityAsset)
                     .OrderBy(value => value.ContentDefinitionId, StringComparer.Ordinal))
        {
            string stableId = ResolveBuildingStableId(building);
            IReadOnlyList<ItemAmountDefinition> currentMaterials =
                building.GetConstructionMaterials();
            if (currentMaterials.Count == 0)
                continue;
            Dictionary<string, string> amountPaths =
                FindConstructionMaterialAmountPaths(building);
            ItemAmountDefinition[] beforeMaterials = currentMaterials
                .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .Select(value => new ItemAmountDefinition(
                    value.ItemId,
                    ResolveHistoricalIntegerBefore(
                        stableId,
                        ConstructionMaterialMetric(value.ItemId),
                        value.Amount,
                        historicalBeforeValues)))
                .ToArray();
            decimal beforeWu = BalanceCanonicalText.DecimalFromFiniteFloat(
                work.CalculateConstruction(building, beforeMaterials),
                $"building:{stableId}:constructionWU");
            decimal beforeBomEwu = 0m;
            bool resolved = true;
            foreach (ItemAmountDefinition material in beforeMaterials)
            {
                if (!beforeValues.TryGetItemWork(material.ItemId, out float oldWork)
                    || !afterValues.Items.ContainsKey(material.ItemId))
                {
                    resolved = false;
                    break;
                }
                beforeBomEwu += BalanceCanonicalText.DecimalFromFiniteFloat(
                    oldWork,
                    $"building:{stableId}:bom") * material.Amount;
            }
            if (!resolved || beforeBomEwu <= 0m)
                continue;

            BuildingWorkAmountAbility authoredWork =
                building.GetAbility<BuildingWorkAmountAbility>();
            decimal? currentApprovedWu = authoredWork != null
                ? BalanceCanonicalText.DecimalFromFiniteFloat(
                    authoredWork.constructionWorkRequired,
                    $"building:{stableId}:currentConstructionWU")
                : null;
            V27ConstructionRedistributionResult selected =
                V27ConstructionRedistributionPolicy.Select(
                    stableId,
                    building,
                    beforeWu,
                    beforeBomEwu,
                    beforeMaterials,
                    afterValues.Items,
                    currentMaterials,
                    currentApprovedWu);
            if (!results.TryAdd(stableId, selected))
                throw new InvalidOperationException($"Duplicate construction result: {stableId}.");

            decimal periodWu = selected.PeriodCandidateWu;
            decimal selectedWu = selected.AfterWu;
            decimal originalAfterBomEwu = selected.BeforeBomMilliEwu / 1000m;
            decimal selectedAfterBomEwu = selected.AfterBomMilliEwu / 1000m;
            decimal beforeDensity = beforeWu / beforeBomEwu;
            decimal periodDensity = periodWu / originalAfterBomEwu;
            decimal selectedDensity = selectedWu / selectedAfterBomEwu;
            BalanceAnomalySeverity selectedSeverity = selected.Disposition switch
            {
                V27ConstructionRedistributionDisposition.Normal =>
                    BalanceAnomalySeverity.None,
                V27ConstructionRedistributionDisposition.CriticalDensityUnresolved =>
                    BalanceAnomalySeverity.Critical,
                _ => BalanceAnomalySeverity.Warning
            };
            string path = AssetDatabase.GetAssetPath(building);
            string sourceDigest = GetSourceDigest(path, sourceDigests);
            string beforeBom = FormatBom(beforeMaterials);
            string currentBom = FormatBom(currentMaterials);
            string afterBom = FormatBom(selected.AfterMaterials);
            long currentBomMilliEwu = currentMaterials.Sum(value => checked(
                afterValues.Items[value.ItemId].AcquisitionCost.MilliEwu
                * value.Amount));
            decimal currentBomEwu = currentBomMilliEwu / 1000m;
            string[] dependencies = beforeMaterials.Select(value => value.ItemId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string authoredWorkPath = authoredWork != null
                ? FindUniqueSerializedPropertyPath(building, "constructionWorkRequired")
                : string.Empty;
            CaptureBuildingCandidate(
                capture, building, stableId, path, sourceDigest, dependencies,
                "construction-wu:period-preserving", beforeWu, periodWu,
                beforeBom, beforeBom,
                beforeBomEwu, originalAfterBomEwu, beforeDensity, periodDensity,
                BalanceAnomalyDetector.ClassifyLaborDensity(periodDensity / beforeDensity),
                "candidate-period-preserving",
                "ceil(V23 runtime WU*2.25); BOM unchanged",
                false,
                "derived:V23BalanceWorkCalculator.CalculateConstruction",
                "candidate");
            CaptureBuildingCandidate(
                capture, building, stableId, path, sourceDigest, dependencies,
                "construction-wu:bom-redistribution", beforeWu, selectedWu,
                beforeBom, afterBom,
                beforeBomEwu, selectedAfterBomEwu, beforeDensity, selectedDensity,
                selectedSeverity,
                "facility-bounded-cost-redistribution",
                selected.SelectionReason,
                false,
                "derived:V27ConstructionRedistributionPolicy",
                "selected");
            CaptureBuildingCandidate(
                capture, building, stableId, path, sourceDigest, dependencies,
                "construction-wu:min-change", beforeWu, selectedWu,
                beforeBom, afterBom,
                beforeBomEwu, selectedAfterBomEwu, beforeDensity, selectedDensity,
                selectedSeverity,
                "facility-bounded-minimum-change",
                selected.SelectionReason + "; changedRows="
                    + CountChangedMaterialRows(selected).ToString(CultureInfo.InvariantCulture)
                    + "; investmentError="
                    + selected.InvestmentErrorMilliEwu.ToString(CultureInfo.InvariantCulture)
                    + "mEWU",
                false,
                "derived:V27ConstructionRedistributionPolicy",
                "selected");
            CaptureBuildingCandidate(
                capture, building, stableId, path, sourceDigest, dependencies,
                "construction-wu:approved", beforeWu, selectedWu,
                beforeBom, afterBom,
                beforeBomEwu, selectedAfterBomEwu, beforeDensity, selectedDensity,
                selectedSeverity,
                "facility-bounded-approved-authority",
                "SELECTED: " + selected.SelectionReason,
                false,
                "BuildingWorkAmountAbility.constructionWorkRequired + constructionMaterials",
                "selected");
            if (authoredWork != null)
            {
                decimal authoredCurrent = BalanceCanonicalText.DecimalFromFiniteFloat(
                    authoredWork.constructionWorkRequired,
                    $"building:{stableId}:constructionWorkRequired");
                decimal patchBefore = ResolveHistoricalAuthoredBefore(
                    stableId,
                    "construction-authored-wu:redistributed",
                    authoredCurrent,
                    historicalBeforeValues);
                string authoredCurrentToken = Token(authoredCurrent);
                string patchBeforeToken = Token(patchBefore);
                string authoredApprovalIdentity =
                    V27BalanceAssetApplication.BuildHistoricalBeforeKey(
                        stableId,
                        "construction-authored-wu:redistributed");
                bool previouslyApprovedCurrent = historicalBeforeValues.TryGetValue(
                        authoredApprovalIdentity,
                        out string approvedWuBefore)
                    && previouslyApprovedAfterValues.TryGetValue(
                        authoredApprovalIdentity,
                        out string approvedWuAfter)
                    && string.Equals(
                        approvedWuBefore,
                        patchBeforeToken,
                        StringComparison.Ordinal)
                    && string.Equals(
                        approvedWuAfter,
                        authoredCurrentToken,
                        StringComparison.Ordinal);
                if (authoredCurrent != patchBefore
                    && authoredCurrent != selectedWu
                    && !previouslyApprovedCurrent)
                {
                    throw new InvalidOperationException(
                        $"Building authored construction WU drifted outside its V27 patch: "
                        + $"{stableId}; current={Token(authoredCurrent)}, "
                        + $"before={Token(patchBefore)}, after={Token(selectedWu)}.");
                }
                bool hasPendingWuRecalibration = previouslyApprovedCurrent
                    && authoredCurrent != selectedWu;
                if (hasPendingWuRecalibration)
                {
                    decimal currentDensity = authoredCurrent / currentBomEwu;
                    CaptureBuildingCandidate(
                        capture, building, stableId, path, sourceDigest, dependencies,
                        "construction-authored-wu:redistributed",
                        patchBefore, authoredCurrent,
                        beforeBom, currentBom,
                        beforeBomEwu, currentBomEwu,
                        beforeDensity, currentDensity,
                        BalanceAnomalySeverity.None,
                        "facility-authored-runtime-wu-authority",
                        "Previously approved runtime WU retained as exact current authority.",
                        true,
                        authoredWorkPath,
                        "applied",
                        true);
                    CaptureBuildingCandidate(
                        capture, building, stableId, path, sourceDigest, dependencies,
                        ConstructionRecalibrationCandidateWuMetric,
                        authoredCurrent, selectedWu,
                        currentBom, afterBom,
                        currentBomEwu, selectedAfterBomEwu,
                        currentDensity, selectedDensity,
                        BalanceAnomalySeverity.Critical,
                        "previous-applied-recalibration-review-required",
                        "Pending recalibration candidate; explicit review must replace the previous approval before any asset mutation.",
                        false,
                        authoredWorkPath,
                        "pending-explicit-review",
                        false,
                        true);
                    anomalies.Add(BalanceAnomalyNode.Capture(
                        stableId,
                        ConstructionRecalibrationCandidateWuMetric,
                        BalanceAnomalySeverity.Critical,
                        BalanceAnomalyDisposition.RootCritical,
                        "previous-applied-recalibration-review-required",
                        Array.Empty<string>()));
                }
                else
                {
                    CaptureBuildingCandidate(
                        capture, building, stableId, path, sourceDigest, dependencies,
                        "construction-authored-wu:redistributed",
                        patchBefore, selectedWu,
                        beforeBom, afterBom,
                        beforeBomEwu, selectedAfterBomEwu,
                        beforeDensity, selectedDensity,
                        selectedSeverity,
                        "facility-authored-runtime-wu-authority",
                        "Approved per-building runtime WU selected by the bounded WU/BOM optimizer.",
                        patchBefore != selectedWu,
                        authoredWorkPath,
                        "pending",
                        authoredCurrent == selectedWu);
                }

                Dictionary<string, int> currentAmounts = currentMaterials
                    .ToDictionary(value => value.ItemId, value => value.Amount, StringComparer.Ordinal);
                Dictionary<string, int> beforeAmounts = beforeMaterials
                    .ToDictionary(value => value.ItemId, value => value.Amount, StringComparer.Ordinal);
                Dictionary<string, int> afterAmounts = selected.AfterMaterials
                    .ToDictionary(value => value.ItemId, value => value.Amount, StringComparer.Ordinal);
                foreach (string itemId in dependencies)
                {
                    int currentAmount = currentAmounts[itemId];
                    int beforeAmount = beforeAmounts[itemId];
                    int afterAmount = afterAmounts[itemId];
                    string materialApprovalIdentity =
                        V27BalanceAssetApplication.BuildHistoricalBeforeKey(
                            stableId,
                            ConstructionMaterialMetric(itemId));
                    bool previouslyApprovedAmount = historicalBeforeValues.TryGetValue(
                            materialApprovalIdentity,
                            out string approvedAmountBefore)
                        && previouslyApprovedAfterValues.TryGetValue(
                            materialApprovalIdentity,
                            out string approvedAmountAfter)
                        && string.Equals(
                            approvedAmountBefore,
                            beforeAmount.ToString(CultureInfo.InvariantCulture),
                            StringComparison.Ordinal)
                        && string.Equals(
                            approvedAmountAfter,
                            currentAmount.ToString(CultureInfo.InvariantCulture),
                            StringComparison.Ordinal);
                    if (currentAmount != beforeAmount
                        && currentAmount != afterAmount
                        && !previouslyApprovedAmount)
                    {
                        throw new InvalidOperationException(
                            $"Building construction BOM drifted outside its V27 patch: "
                            + $"{stableId}:{itemId}; current={currentAmount}; "
                            + $"before={beforeAmount}; after={afterAmount}.");
                    }
                    bool hasPendingAmountRecalibration = previouslyApprovedAmount
                        && currentAmount != afterAmount;
                    if (hasPendingAmountRecalibration)
                    {
                        CaptureBuildingMaterialAmount(
                            capture,
                            stableId,
                            path,
                            sourceDigest,
                            dependencies,
                            itemId,
                            ConstructionMaterialMetric(itemId),
                            beforeAmount,
                            currentAmount,
                            beforeBom,
                            currentBom,
                            beforeBomEwu,
                            currentBomEwu,
                            amountPaths[itemId],
                            BalanceAnomalySeverity.None,
                            "facility-bounded-bom-redistribution",
                            "Previously approved physical BOM amount retained as exact current authority.",
                            "applied",
                            true,
                            true,
                            false,
                            beforeAmount);
                        string candidateMetric =
                            ConstructionRecalibrationCandidateMaterialMetricPrefix + itemId;
                        CaptureBuildingMaterialAmount(
                            capture,
                            stableId,
                            path,
                            sourceDigest,
                            dependencies,
                            itemId,
                            candidateMetric,
                            currentAmount,
                            afterAmount,
                            currentBom,
                            afterBom,
                            currentBomEwu,
                            selectedAfterBomEwu,
                            amountPaths[itemId],
                            BalanceAnomalySeverity.Critical,
                            "previous-applied-recalibration-review-required",
                            "Pending BOM recalibration candidate; explicit review must replace the previous approval before any asset mutation.",
                            "pending-explicit-review",
                            false,
                            false,
                            true,
                            beforeAmount);
                        anomalies.Add(BalanceAnomalyNode.Capture(
                            stableId,
                            candidateMetric,
                            BalanceAnomalySeverity.Critical,
                            BalanceAnomalyDisposition.RootCritical,
                            "previous-applied-recalibration-review-required",
                            Array.Empty<string>()));
                    }
                    else
                    {
                        CaptureBuildingMaterialAmount(
                            capture,
                            stableId,
                            path,
                            sourceDigest,
                            dependencies,
                            itemId,
                            ConstructionMaterialMetric(itemId),
                            beforeAmount,
                            afterAmount,
                            beforeBom,
                            afterBom,
                            beforeBomEwu,
                            selectedAfterBomEwu,
                            amountPaths[itemId],
                            BalanceAnomalySeverity.None,
                            "facility-bounded-bom-redistribution",
                            afterAmount == beforeAmount
                                ? "Existing physical BOM amount retained."
                                : "Existing physical BOM amount increased within the 50% cap; no new item type.",
                            beforeAmount == afterAmount ? "unchanged" : "pending",
                            true,
                            currentAmount == afterAmount,
                            false,
                            beforeAmount);
                    }
                }
            }
            if (selectedSeverity == BalanceAnomalySeverity.Warning)
            {
                anomalies.Add(BalanceAnomalyNode.Capture(
                    stableId,
                    "labor-density",
                    BalanceAnomalySeverity.Warning,
                    BalanceAnomalyDisposition.None,
                    "bounded-redistribution-warning",
                    dependencies));
            }
            else if (selectedSeverity == BalanceAnomalySeverity.Critical)
            {
                if (!anomalies.Any(value =>
                        string.Equals(value.StableId, stableId, StringComparison.Ordinal)
                        && string.Equals(
                            value.Metric,
                            ConstructionRecalibrationCandidateWuMetric,
                            StringComparison.Ordinal)))
                {
                    anomalies.Add(BalanceAnomalyNode.Capture(
                        stableId,
                        "construction-authored-wu:redistributed",
                        BalanceAnomalySeverity.Critical,
                        BalanceAnomalyDisposition.RootCritical,
                        "construction-density-unresolved-within-bounds",
                        Array.Empty<string>()));
                }
            }
        }
        return results;
    }

    private static void CaptureIntegratedCapacityMetrics(
        BalanceCaptureFactory capture,
        IDictionary<string, string> sourceDigests)
    {
        const string survivalSource =
            "Assets/Scripts/Services/Economy/V27SurvivalClosedLoopModels.cs";
        const string spatialSource =
            "Assets/Scripts/Services/Economy/Editor/V27AssetBackedSpatialCapacityDebugScenarios.cs";
        const string continuitySource =
            "Assets/Scripts/Services/Economy/SurvivalContinuityCatalogQuery.cs";
        string survivalDigest = GetSourceDigest(survivalSource, sourceDigests);
        string spatialDigest = GetSourceDigest(spatialSource, sourceDigests);
        string continuityDigest = GetSourceDigest(continuitySource, sourceDigests);

        foreach (int population in PopulationStagePortfolioCatalog.PopulationStages)
        {
            SurvivalClosedLoopAssessment value =
                V27SixAdultSurvivalLoopDebugScenarios.CapturePopulationStage(population);
            if (!value.Passed)
                throw new InvalidOperationException(value.FailureCode);
            string stableId = "population-stage:" + Token(population);
            CaptureInvariantMetric(capture, "survival", "population-stage", stableId,
                "daily-food-demand", "milli-nutrition/day",
                value.DailyFoodDemandMilliNutrition, survivalSource, survivalDigest,
                "population * authored hunger depletion", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "survival", "population-stage", stableId,
                "gross-food-coverage", "permille",
                value.GrossFoodCoveragePermille, survivalSource, survivalDigest,
                "gross physical nutrition / daily demand", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "survival", "population-stage", stableId,
                "net-food-coverage", "permille",
                value.NetFoodCoveragePermille, survivalSource, survivalDigest,
                "post-loss physical nutrition / daily demand", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "survival", "population-stage", stableId,
                "gross-food-target", "milli-nutrition/day",
                value.GrossFoodTargetMilliNutrition, survivalSource, survivalDigest,
                "ceil daily demand * 1.25", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "survival", "population-stage", stableId,
                "net-food-target", "milli-nutrition/day",
                value.NetFoodTargetMilliNutrition, survivalSource, survivalDigest,
                "ceil daily demand * 1.10", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "survival", "population-stage", stableId,
                "gross-food-produced", "milli-nutrition/day",
                value.GrossFoodProducedMilliNutrition, survivalSource, survivalDigest,
                "physical crop and meal throughput", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "survival", "population-stage", stableId,
                "net-food-produced", "milli-nutrition/day",
                value.NetFoodProducedMilliNutrition, survivalSource, survivalDigest,
                "gross production minus authored loss", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "survival", "population-stage", stableId,
                "drinking-water-demand", "milli-units/day",
                value.DrinkingWaterDemandMilliUnitsPerDay, survivalSource, survivalDigest,
                "population thirst / safe drink recovery", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "survival", "population-stage", stableId,
                "gross-water-coverage", "permille",
                value.GrossDrinkingWaterCoveragePermille, survivalSource, survivalDigest,
                "gross physical clean-water / drinking demand", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "labor", "population-stage", stableId,
                "recurring-survival-work", "mWU/day",
                value.RecurringMilliWuPerDay, survivalSource, survivalDigest,
                "crop + cooking + clean-water recurring work", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "labor", "population-stage", stableId,
                "recurring-survival-share", "permille",
                value.RecurringSharePermille, survivalSource, survivalDigest,
                "recurring work / population effective work", SixAdultClosedLoopBaselineRecordId);
            long effectiveMilliWu = checked(population * 45000L);
            const int logisticsReservePermille = 150;
            const int emergencyReservePermille = 100;
            int growthAvailablePermille = checked(
                1000 - value.RecurringSharePermille
                - logisticsReservePermille - emergencyReservePermille);
            CaptureInvariantMetric(capture, "labor", "population-stage", stableId,
                "effective-work-capacity", "mWU/day", effectiveMilliWu,
                survivalSource, survivalDigest, "population * 45 effective WU/day",
                SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "labor", "population-stage", stableId,
                "crop-work", "mWU/day", value.CropMilliWuPerDay,
                survivalSource, survivalDigest, "sow + harvest recurring work",
                SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "labor", "population-stage", stableId,
                "cooking-work", "mWU/day", value.CookingMilliWuPerDay,
                survivalSource, survivalDigest, "physical meal cycles * direct work",
                SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "labor", "population-stage", stableId,
                "water-work", "mWU/day", value.WaterMilliWuPerDay,
                survivalSource, survivalDigest, "clean-water cycles * direct work",
                SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "labor", "population-stage", stableId,
                "growth-labor-available", "permille", growthAvailablePermille,
                survivalSource, survivalDigest,
                "1000 - recurring - logistics(150) - emergency(100)",
                PopulationCapacityBaselineRecordId);
            CaptureInvariantMetric(capture, "labor", "population-stage", stableId,
                "emergency-labor-reserve", "permille", emergencyReservePermille,
                survivalSource, survivalDigest, "authored minimum emergency reserve",
                PopulationCapacityBaselineRecordId);
            CaptureInvariantMetric(capture, "survival", "population-stage", stableId,
                "immediate-meal-buffer", "units",
                value.ImmediateMealUnits, survivalSource, survivalDigest,
                "one-day meal demand rounded to physical recipe batch", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "storage", "population-stage", stableId,
                "seven-day-grain-reserve", "units",
                value.SevenDayGrainUnits, survivalSource, survivalDigest,
                "seven-day food demand converted to physical grain", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "storage", "population-stage", stableId,
                "seven-day-clean-water-reserve", "units",
                value.SevenDayWaterUnits, survivalSource, survivalDigest,
                "seven-day total clean-water demand", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "storage", "population-stage", stableId,
                "required-storage-mass", "grams",
                value.RequiredStorageMassGrams, survivalSource, survivalDigest,
                "physical reserve quantities multiplied by canonical item gram authority", OverflowContainmentBaselineRecordId);
            CaptureInvariantMetric(capture, "agriculture", "population-stage", stableId,
                "required-crop-plots", "plots",
                value.CropPlots, survivalSource, survivalDigest,
                "ceil gross grain demand / daily physical crop yield", SixAdultClosedLoopBaselineRecordId);
            CaptureInvariantMetric(capture, "space", "population-stage", stableId,
                "expansion-tier", "tier",
                PopulationStagePortfolioCatalog.TierForPopulation(population),
                survivalSource, survivalDigest,
                "authored research-gated capacity tier; developer E-key excluded",
                PopulationCapacityBaselineRecordId);
        }

        IReadOnlyList<V27AssetBackedStageCapacityAssessment> spatial =
            V27AssetBackedSpatialCapacityDebugScenarios.CaptureStageCapacityAssessments();
        foreach (V27AssetBackedStageCapacityAssessment value in spatial)
        {
            string stableId = "population-stage:" + Token(value.Population);
            CaptureInvariantMetric(capture, "space", "population-stage", stableId,
                "interior-columns", "columns", value.InteriorColumns,
                spatialSource, spatialDigest, "minimum passing width across 256 deterministic layouts",
                PopulationCapacityBaselineRecordId);
            CaptureInvariantMetric(capture, "space", "population-stage", stableId,
                "successful-layout-seeds", "seeds", value.SuccessfulSeeds,
                spatialSource, spatialDigest, "asset-backed BuildingPlacementValidator successes",
                PopulationCapacityBaselineRecordId);
            CaptureInvariantMetric(capture, "space", "population-stage", stableId,
                "minimum-headroom", "permille", value.MinimumHeadroomPermille,
                spatialSource, spatialDigest, "usable cells minus union-accounted occupied cells",
                SharedAccessBaselineRecordId);
            CaptureInvariantMetric(capture, "space", "population-stage", stableId,
                "maximum-normal-cell-utilization", "permille",
                value.MaximumNormalCellUtilizationPermille, spatialSource, spatialDigest,
                "maximum shared access/corridor utilization", SharedAccessBaselineRecordId);
            CaptureInvariantMetric(capture, "space", "population-stage", stableId,
                "maximum-fault-cell-utilization", "permille",
                value.MaximumFaultCellUtilizationPermille, spatialSource, spatialDigest,
                "single-fault shared access/corridor utilization", SharedAccessBaselineRecordId);
            CaptureInvariantMetric(capture, "storage", "population-stage", stableId,
                "maximum-normal-storage-utilization", "permille",
                value.MaximumNormalStorageUtilizationPermille, spatialSource, spatialDigest,
                "normal physical reserve / installed storage capacity", OverflowContainmentBaselineRecordId);
            CaptureInvariantMetric(capture, "storage", "population-stage", stableId,
                "maximum-fault-storage-utilization", "permille",
                value.MaximumFaultStorageUtilizationPermille, spatialSource, spatialDigest,
                "single-fault reserve and burst / installed storage capacity", OverflowContainmentBaselineRecordId);
            CaptureInvariantMetric(capture, "space", "population-stage", stableId,
                "facility-requirement-count", "requirements", value.FacilityRequirementCount,
                spatialSource, spatialDigest, "population portfolio physical facility count",
                PopulationCapacityBaselineRecordId);
            CaptureInvariantMetric(capture, "storage", "population-stage", stableId,
                "minimum-storage-capacity", "grams", value.MinimumStorageCapacityGrams,
                spatialSource, spatialDigest, "minimum installed physical mass capacity",
                OverflowContainmentBaselineRecordId);
            CaptureInvariantMetric(capture, "space", "population-stage", stableId,
                "fixed-interior-world-feature-cells", "cells",
                V27PopulationStageSpatialBaseline.FixedWorldFeatureCells(value.Population),
                spatialSource, spatialDigest,
                "live interior resource-node cross-check authority", PopulationCapacityBaselineRecordId);
            CaptureInvariantMetric(capture, "space", "population-stage", stableId,
                "maximum-effective-used-cells", "cells", value.MaximumUsedCells,
                spatialSource, spatialDigest, "exclusive U shared operational U overflow U fixed",
                SharedAccessBaselineRecordId);
            CaptureInvariantMetric(capture, "space", "population-stage", stableId,
                "maximum-exclusive-footprint-cells", "cells", value.MaximumExclusiveCells,
                spatialSource, spatialDigest, "union of authored physical footprints",
                SharedAccessBaselineRecordId);
            CaptureInvariantMetric(capture, "space", "population-stage", stableId,
                "maximum-raw-access-cells", "cells", value.MaximumRawAccessCells,
                spatialSource, spatialDigest, "sum of facility access cells before overlap",
                SharedAccessBaselineRecordId);
            CaptureInvariantMetric(capture, "space", "population-stage", stableId,
                "maximum-shared-operational-union-cells", "cells", value.MaximumSharedAccessCells,
                spatialSource, spatialDigest, "union of access, queue and shared corridor cells",
                SharedAccessBaselineRecordId);
            CaptureInvariantMetric(capture, "space", "population-stage", stableId,
                "minimum-access-overlap-savings", "cells", value.MinimumAccessOverlapSavings,
                spatialSource, spatialDigest, "raw access sum - shared operational union",
                SharedAccessBaselineRecordId);
            CaptureInvariantMetric(capture, "storage", "population-stage", stableId,
                "overflow-containment-cells", "cells", value.OverflowCells,
                spatialSource, spatialDigest, "largest single-fault production burst containment",
                OverflowContainmentBaselineRecordId);
        }

        foreach (SurvivalContinuityPathSnapshot path in
                 V27SixAdultSurvivalLoopDebugScenarios.CaptureContinuityPaths(6))
        {
            string stableId = path.PathId;
            CaptureInvariantMetric(capture, "continuity", "service-path", stableId,
                "capacity", "permille", path.CapacityPermille,
                continuitySource, continuityDigest, "authored production path capacity",
                path.IsPrimitive ? PrimitiveFallbackBaselineRecordId : ServiceContinuityBaselineRecordId);
            CaptureInvariantMetric(capture, "continuity", "service-path", stableId,
                "recurring-work", "mWU/day", path.RecurringMilliWuPerDay,
                continuitySource, continuityDigest, "service-path recurring work",
                path.IsPrimitive ? PrimitiveFallbackBaselineRecordId : ServiceContinuityBaselineRecordId);
            CaptureInvariantMetric(capture, "continuity", "service-path", stableId,
                "action-duration", "milliseconds", path.ActionDurationMilliseconds,
                continuitySource, continuityDigest, "production action duration",
                path.IsPrimitive ? PrimitiveFallbackBaselineRecordId : ServiceContinuityBaselineRecordId);
            CaptureInvariantMetric(capture, "continuity", "service-path", stableId,
                "physical-input-quantity", "units", path.PhysicalInputQuantity,
                continuitySource, continuityDigest, "exact physical input consumption",
                path.IsPrimitive ? PrimitiveFallbackBaselineRecordId : ServiceContinuityBaselineRecordId);
            CaptureInvariantMetric(capture, "continuity", "service-path", stableId,
                "mood-delta", "milli-units", path.MoodDeltaMilliUnits,
                continuitySource, continuityDigest, "service-path authored consequence",
                path.IsPrimitive ? PrimitiveFallbackBaselineRecordId : ServiceContinuityBaselineRecordId);
            CaptureInvariantMetric(capture, "continuity", "service-path", stableId,
                "hygiene-delta", "milli-units", path.HygieneDeltaMilliUnits,
                continuitySource, continuityDigest, "service-path authored consequence",
                path.IsPrimitive ? PrimitiveFallbackBaselineRecordId : ServiceContinuityBaselineRecordId);
            CaptureInvariantMetric(capture, "continuity", "service-path", stableId,
                "waste-output", "milli-units", path.WasteMilliUnits,
                continuitySource, continuityDigest, "service-path authored consequence",
                path.IsPrimitive ? PrimitiveFallbackBaselineRecordId : ServiceContinuityBaselineRecordId);
            CaptureInvariantMetric(capture, "continuity", "service-path", stableId,
                "stain-output", "milli-units", path.StainMilliUnits,
                continuitySource, continuityDigest, "service-path authored consequence",
                path.IsPrimitive ? PrimitiveFallbackBaselineRecordId : ServiceContinuityBaselineRecordId);
            CaptureInvariantMetric(capture, "continuity", "service-path", stableId,
                "primitive-fallback-role", "boolean", path.IsPrimitive ? 1 : 0,
                continuitySource, continuityDigest,
                "1=fallback primitive path; 0=primary facility path",
                path.IsPrimitive ? PrimitiveFallbackBaselineRecordId : ServiceContinuityBaselineRecordId);
        }

        foreach (ServiceContinuityRequirement requirement in
                 PopulationStagePortfolioCatalog.Capture(6).CriticalServices)
        {
            string stableId = "service-continuity:" + requirement.ServiceId;
            CaptureInvariantMetric(capture, "continuity", "service-requirement", stableId,
                "outage-coverage", "hours", requirement.OutageCoverageHours,
                continuitySource, continuityDigest,
                "single primary-path outage covered by an independent production fallback",
                ServiceContinuityBaselineRecordId);
            CaptureInvariantMetric(capture, "continuity", "service-requirement", stableId,
                "primary-fallback-independent", "boolean",
                string.Equals(
                    requirement.PrimaryPathId,
                    requirement.FallbackPathId,
                    StringComparison.Ordinal) ? 0 : 1,
                continuitySource, continuityDigest,
                "primaryPathId != fallbackPathId",
                ServiceContinuityBaselineRecordId);
        }

        V27RedundancyCapitalAssessment capital =
            V27AssetBackedSpatialCapacityDebugScenarios.CaptureSixAdultRedundancyCapital();
        CaptureInvariantMetric(capture, "capital", "population-stage",
            "population-stage:6", "service-redundancy-capital-ratio", "permille",
            capital.ActualRedundancyCapitalPermille, spatialSource, spatialDigest,
            "actual duplicate service capital / total six-adult facility capital",
            ServiceContinuityBaselineRecordId);
        CaptureInvariantMetric(capture, "capital", "population-stage",
            "population-stage:6", "avoided-duplicate-service-capital", "milli-capital",
            capital.AvoidedDuplicateCapitalMilliUnits, spatialSource, spatialDigest,
            "primitive N+1 avoids duplicate food-production and water facility capital",
            PrimitiveFallbackBaselineRecordId);
        CaptureInvariantMetric(capture, "capital", "population-stage",
            "population-stage:6", "actual-redundancy-bom", "milli-capital",
            capital.ActualRedundancyBomMilliUnits, spatialSource, spatialDigest,
            "construction BOM portion of installed duplicate service capacity",
            ServiceContinuityBaselineRecordId);
        CaptureInvariantMetric(capture, "capital", "population-stage",
            "population-stage:6", "actual-redundancy-work", "mWU",
            capital.ActualRedundancyWorkMilliUnits, spatialSource, spatialDigest,
            "construction work portion of installed duplicate service capacity",
            ServiceContinuityBaselineRecordId);
        CaptureInvariantMetric(capture, "capital", "population-stage",
            "population-stage:6", "avoided-duplicate-service-bom", "milli-capital",
            capital.AvoidedDuplicateBomMilliUnits, spatialSource, spatialDigest,
            "BOM avoided by primitive N+1 paths",
            PrimitiveFallbackBaselineRecordId);
        CaptureInvariantMetric(capture, "capital", "population-stage",
            "population-stage:6", "avoided-duplicate-service-work", "mWU",
            capital.AvoidedDuplicateWorkMilliUnits, spatialSource, spatialDigest,
            "construction work avoided by primitive N+1 paths",
            PrimitiveFallbackBaselineRecordId);

        CapturePairedRunMetrics(capture);
        CaptureRandomStreamMetrics(capture);
        CaptureOutputCapacityMetrics(capture);
    }

    private static void CaptureOutputCapacityMetrics(
        BalanceCaptureFactory capture)
    {
        string path = V27OutputCapacityEvidenceDebugScenarios.ReportPath;
        if (!File.Exists(path))
            throw new InvalidOperationException(
                "V27 output-capacity PlayMode evidence is missing.");
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        string expectedSourceDigest = V27OutputCapacityEvidenceDebugScenarios
            .ComputeEvidenceSourceDigest();
        if (!lines.Contains("RESULT=PASS; checks=2; failures=0", StringComparer.Ordinal)
            || !lines.Contains(
                "sourceDigest=" + expectedSourceDigest,
                StringComparer.Ordinal)
            || !lines.Any(line => line.StartsWith(
                "PASS WORLD_RESOURCE_EXACT_SOURCE_ATOMIC_PUBLICATION ",
                StringComparison.Ordinal))
            || !lines.Any(line => line.StartsWith(
                "PASS CROP_OUTPUT_FACILITY_BUFFER_WAIT_RESTORE_RETRY_EXACT_ONCE ",
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "V27 output-capacity PlayMode evidence is stale or incomplete.");
        }

        CaptureInvariantMetric(capture, "production", "output-capacity",
            "output-capacity:world-resource", "exact-source-atomic-publication", "boolean", 1,
            path, expectedSourceDigest,
            "physical source output freezes once and commits with its source debit exactly once",
            OutputContainmentBaselineRecordId);
        CaptureInvariantMetric(capture, "agriculture", "output-capacity",
            "output-capacity:crop-harvest", "buffer-wait-restore-retry-exact-once", "boolean", 1,
            path, expectedSourceDigest,
            "harvest output waits in a frozen physical batch and restores/retries exactly once",
            OutputContainmentBaselineRecordId);
    }

    private static void CapturePairedRunMetrics(BalanceCaptureFactory capture)
    {
        const string path = "Artifacts/QA/v27-balance-paired-run-rng.txt";
        string[] lines = File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
        if (lines.Length == 0
            || !lines[0].StartsWith("RESULT=PASS;", StringComparison.Ordinal))
        {
            CaptureFocusedPairedRunMetrics(capture);
            return;
        }
        string sourceDigest = HashText(File.ReadAllText(path));
        CaptureInvariantMetric(capture, "chaos", "paired-run", "paired-run:four-arm",
            "seed-count", "seeds", ParseKey(lines[0], "seeds"), path, sourceDigest,
            "cleanRepeatA/cleanRepeatB/faultControl/clutterStress", PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "chaos", "paired-run", "paired-run:four-arm",
            "window-count", "windows", ParseKey(lines[0], "windows"), path, sourceDigest,
            "fixed game-time windows", PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "chaos", "paired-run", "paired-run:four-arm",
            "floor-diagnostic-row-count", "rows", ParseKey(lines[0], "floorRows"), path, sourceDigest,
            "per-arm per-window floor diagnostics", FloorClutterBaselineRecordId);
        string attribution = RequireReportLine(lines, "PASS\tPAIRED_CLUTTER_ATTRIBUTION\t");
        CaptureInvariantMetric(capture, "chaos", "paired-run", "paired-run:four-arm",
            "wait-wu-delta-median", "permille", ParseKey(attribution, "medianPermille"),
            path, sourceDigest, "median (clutterStress-faultControl)/faultControl wait WU",
            PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "chaos", "paired-run", "paired-run:four-arm",
            "wait-wu-delta-p95", "permille", ParseKey(attribution, "p95Permille"),
            path, sourceDigest, "p95 (clutterStress-faultControl)/faultControl wait WU",
            PairedRunBaselineRecordId);
        string clutter = RequireReportLine(lines, "PASS\tFLOOR_CLUTTER_RECOVERY_ZERO\t");
        CaptureInvariantMetric(capture, "chaos", "paired-run", "paired-run:four-arm",
            "persistent-floor-clutter", "stacks", ParseKey(clutter, "persistent"),
            path, sourceDigest, "persistent loose stacks outside authorized containment",
            FloorClutterBaselineRecordId);
        string headroom = RequireReportLine(lines, "PASS\tPAIRED_RUNTIME_HEADROOM_AT_LEAST_30_PERCENT\t");
        CaptureInvariantMetric(capture, "chaos", "paired-run", "paired-run:four-arm",
            "minimum-runtime-headroom", "permille", ParseKey(headroom, "minimumPermille"),
            path, sourceDigest, "minimum runtime headroom across four arms",
            FloorClutterBaselineRecordId);
        string crossTalk = RequireReportLine(lines, "PASS\tRNG_CAUSAL_CONE_NO_CROSS_TALK\t");
        CaptureInvariantMetric(capture, "rng", "paired-run", "paired-run:four-arm",
            "outside-causal-cone-divergence", "streams",
            ParseKey(crossTalk, "outsideConeDivergence"), path, sourceDigest,
            "unaffected stream divergence", CounterfactualRngBaselineRecordId);

        const string pairedCsvPath = "Artifacts/QA/v27-balance-paired-run-rng.csv";
        const string floorCsvPath = "Artifacts/QA/v27-balance-floor-clutter.csv";
        long[] dispatch = ReadIntegerCsvColumn(pairedCsvPath, "dispatchWaitMilliWu");
        long[] reservation = ReadIntegerCsvColumn(pairedCsvPath, "reservationWaitMilliWu");
        long[] access = ReadIntegerCsvColumn(pairedCsvPath, "facilityAccessWaitMilliWu");
        long[] noPath = ReadIntegerCsvColumn(pairedCsvPath, "noPathMilliWu");
        long[] replans = ReadIntegerCsvColumn(pairedCsvPath, "replanCount");
        long[] stepAside = ReadIntegerCsvColumn(pairedCsvPath, "stepAsideCount");
        string pairedCsvDigest = HashText(File.ReadAllText(pairedCsvPath));
        CaptureInvariantMetric(capture, "logistics", "paired-run", "paired-run:four-arm",
            "haul-dispatch-wait-p95", "mWU", Percentile95(dispatch),
            pairedCsvPath, pairedCsvDigest, "p95 fixed-window haul dispatch wait",
            PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "logistics", "paired-run", "paired-run:four-arm",
            "reservation-wait-p95", "mWU", Percentile95(reservation),
            pairedCsvPath, pairedCsvDigest, "p95 fixed-window reservation wait",
            PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "logistics", "paired-run", "paired-run:four-arm",
            "facility-access-wait-p95", "mWU", Percentile95(access),
            pairedCsvPath, pairedCsvDigest, "p95 fixed-window facility access wait",
            PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "logistics", "paired-run", "paired-run:four-arm",
            "no-path-wait-p95", "mWU", Percentile95(noPath),
            pairedCsvPath, pairedCsvDigest, "p95 fixed-window no-path wait",
            PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "logistics", "paired-run", "paired-run:four-arm",
            "maximum-replan-count", "events/window", replans.Max(),
            pairedCsvPath, pairedCsvDigest, "maximum replans in one fixed game-time window",
            PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "logistics", "paired-run", "paired-run:four-arm",
            "maximum-step-aside-count", "events/window", stepAside.Max(),
            pairedCsvPath, pairedCsvDigest, "maximum StepAside events in one fixed game-time window",
            PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "rng", "paired-run", "paired-run:four-arm",
            "clean-repeatability-exact", "boolean",
            RequireReportLine(lines, "PASS\tPAIRED_RUN_CLEAN_REPEATABILITY_EXACT\t") != null ? 1 : 0,
            path, sourceDigest, "cleanRepeatA and cleanRepeatB exact semantic/RNG/mWU equality",
            CounterfactualRngBaselineRecordId);
        CaptureInvariantMetric(capture, "logistics", "paired-run", "paired-run:four-arm",
            "burst-quantity-conserved", "boolean",
            RequireReportLine(lines, "PASS\tPAIRED_BURST_QUANTITY_CONSERVED\t") != null ? 1 : 0,
            path, sourceDigest, "all burst rows conserve delivered plus outstanding quantity",
            FloorClutterBaselineRecordId);

        long[] looseStacks = ReadIntegerCsvColumn(floorCsvPath, "looseStacks");
        long[] looseQuantity = ReadIntegerCsvColumn(floorCsvPath, "looseQuantity");
        long[] immediateFailures = ReadIntegerCsvColumn(floorCsvPath, "immediateFailures");
        long[] clutterSeconds = ReadIntegerCsvColumn(floorCsvPath, "clutterCellSeconds");
        string floorCsvDigest = HashText(File.ReadAllText(floorCsvPath));
        CaptureInvariantMetric(capture, "clutter", "paired-run", "paired-run:four-arm",
            "maximum-loose-stack-count", "stacks", looseStacks.Max(),
            floorCsvPath, floorCsvDigest, "maximum observed physical Loose stack count",
            FloorClutterBaselineRecordId);
        CaptureInvariantMetric(capture, "clutter", "paired-run", "paired-run:four-arm",
            "maximum-loose-quantity", "units", looseQuantity.Max(),
            floorCsvPath, floorCsvDigest, "maximum observed physical Loose quantity",
            FloorClutterBaselineRecordId);
        CaptureInvariantMetric(capture, "clutter", "paired-run", "paired-run:four-arm",
            "access-egress-clutter-failures", "cells", immediateFailures.Max(),
            floorCsvPath, floorCsvDigest, "immediate failures on access, egress or stair landing",
            FloorClutterBaselineRecordId);
        CaptureInvariantMetric(capture, "clutter", "paired-run", "paired-run:four-arm",
            "maximum-clutter-cell-seconds", "cell-seconds", clutterSeconds.Max(),
            floorCsvPath, floorCsvDigest, "maximum clutter occupancy in one fixed window",
            FloorClutterBaselineRecordId);
    }

    private static void CaptureFocusedPairedRunMetrics(BalanceCaptureFactory capture)
    {
        string path = V27PairedClutterPlayModeVerifier.FocusedReportPath;
        string pairedCsvPath = V27PairedClutterPlayModeVerifier.FocusedPairedCsvPath;
        string floorCsvPath = V27PairedClutterPlayModeVerifier.FocusedClutterCsvPath;
        if (!File.Exists(path) || !File.Exists(pairedCsvPath) || !File.Exists(floorCsvPath))
            throw new InvalidOperationException("V27 focused paired-run evidence is missing.");
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        string expectedDigest = V27PairedClutterPlayModeVerifier.ComputeEvidenceSourceDigest();
        if (lines.Length == 0
            || !lines[0].StartsWith("RESULT=PASS;", StringComparison.Ordinal)
            || !string.Equals(ParseTextKey(lines[0], "sourceDigest"), expectedDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "V27 focused paired-run evidence is stale or not PASS.");
        }

        RequireReportLine(lines, "PASS\tPAIRED_FOCUSED_FOUR_ARMS\t");
        RequireReportLine(lines, "PASS\tPAIRED_RUN_CLEAN_REPEATABILITY_EXACT\t");
        RequireReportLine(lines, "PASS\tPAIRED_RUN_EXOGENOUS_EVENTS_EXACT\t");
        RequireReportLine(lines, "PASS\tPAIRED_FOCUSED_BURST_QUANTITY_CONSERVED\t");
        string postPickup = RequireReportLine(
            lines,
            "PASS\tPAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP\t");
        string clutter = RequireReportLine(lines, "PASS\tFLOOR_CLUTTER_RECOVERY_ZERO\t");
        string access = RequireReportLine(lines, "PASS\tFLOOR_CLUTTER_ACCESS_EGRESS_ZERO\t");
        string headroom = RequireReportLine(
            lines,
            "PASS\tPAIRED_RUNTIME_HEADROOM_AT_LEAST_30_PERCENT\t");
        string crossTalk = RequireReportLine(
            lines,
            "PASS\tRNG_CAUSAL_CONE_NO_CROSS_TALK\t");

        CaptureInvariantMetric(capture, "chaos", "paired-run-focused",
            "paired-run-focused:four-arm", "seed-count", "seeds",
            ParseKey(lines[0], "seeds"), path, expectedDigest,
            "Ship P0 representative four-arm current-source run",
            PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "chaos", "paired-run-focused",
            "paired-run-focused:four-arm", "window-count", "windows",
            ParseKey(lines[0], "windows"), path, expectedDigest,
            "four fixed game-time windows per arm", PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "logistics", "paired-run-focused",
            "paired-run-focused:four-arm", "post-pickup-fault-arms", "arms",
            ParseKey(postPickup, "arms"), path, expectedDigest,
            "representative Downed fault occurs after physical pickup",
            PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "clutter", "paired-run-focused",
            "paired-run-focused:four-arm", "persistent-floor-clutter", "stacks",
            ParseKey(clutter, "persistent"), path, expectedDigest,
            "persistent loose stacks after recovery", FloorClutterBaselineRecordId);
        CaptureInvariantMetric(capture, "clutter", "paired-run-focused",
            "paired-run-focused:four-arm", "access-egress-clutter-failures", "cells",
            ParseKey(access, "immediateFailures"), path, expectedDigest,
            "access and emergency egress immediate clutter failures",
            FloorClutterBaselineRecordId);
        CaptureInvariantMetric(capture, "space", "paired-run-focused",
            "paired-run-focused:four-arm", "minimum-runtime-headroom", "permille",
            ParseKey(headroom, "minimumPermille"), path, expectedDigest,
            "minimum runtime headroom in representative four-arm run",
            FloorClutterBaselineRecordId);
        CaptureInvariantMetric(capture, "rng", "paired-run-focused",
            "paired-run-focused:four-arm", "outside-causal-cone-divergence", "streams",
            ParseKey(crossTalk, "outsideConeDivergence"), path, expectedDigest,
            "unaffected random stream divergence", CounterfactualRngBaselineRecordId);

        long[] dispatch = ReadIntegerCsvColumn(pairedCsvPath, "dispatchWaitMilliWu");
        long[] noPath = ReadIntegerCsvColumn(pairedCsvPath, "noPathMilliWu");
        long[] clutterSeconds = ReadIntegerCsvColumn(floorCsvPath, "clutterCellSeconds");
        string pairedDigest = HashText(File.ReadAllText(pairedCsvPath));
        string floorDigest = HashText(File.ReadAllText(floorCsvPath));
        CaptureInvariantMetric(capture, "logistics", "paired-run-focused",
            "paired-run-focused:four-arm", "haul-dispatch-wait-window-p95", "mWU",
            Percentile95(dispatch), pairedCsvPath, pairedDigest,
            "representative fixed-window haul dispatch wait p95", PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "logistics", "paired-run-focused",
            "paired-run-focused:four-arm", "no-path-wait-window-p95", "mWU",
            Percentile95(noPath), pairedCsvPath, pairedDigest,
            "representative fixed-window no-path wait p95", PairedRunBaselineRecordId);
        CaptureInvariantMetric(capture, "clutter", "paired-run-focused",
            "paired-run-focused:four-arm", "maximum-clutter-cell-seconds", "cell-seconds",
            clutterSeconds.Max(), floorCsvPath, floorDigest,
            "maximum representative clutter occupancy in one fixed window",
            FloorClutterBaselineRecordId);
    }

    private static void CaptureRandomStreamMetrics(BalanceCaptureFactory capture)
    {
        const string path = "Artifacts/QA/v27-balance-random-stream-manifest.txt";
        if (!File.Exists(path))
            throw new InvalidOperationException("V27 random-stream manifest is missing.");
        string[] lines = File.ReadAllLines(path);
        if (lines.Length == 0
            || !lines[0].StartsWith("RESULT=PASS;", StringComparison.Ordinal))
            throw new InvalidOperationException("V27 random-stream manifest is not PASS.");
        string digest = HashText(File.ReadAllText(path));
        string[] snapshots = lines
            .Where(line => line.StartsWith("SNAPSHOT\t", StringComparison.Ordinal))
            .ToArray();
        if (snapshots.Length == 0)
            throw new InvalidOperationException("V27 random-stream diagnostic snapshots are missing.");
        foreach (string line in snapshots)
        {
            string streamId = ParseTextKey(line, "streamId");
            ulong state = ulong.Parse(
                ParseTextKey(line, "state"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);
            long draws = long.Parse(
                ParseTextKey(line, "drawCount"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);
            string stableId = "random-stream:" + streamId;
            CaptureInvariantMetric(capture, "rng", "random-stream", stableId,
                "state-high32", "uint32", (long)(state >> 32),
                path, digest, "upper 32 bits of deterministic stream state",
                CounterfactualRngBaselineRecordId);
            CaptureInvariantMetric(capture, "rng", "random-stream", stableId,
                "state-low32", "uint32", (long)(state & 0xffffffffUL),
                path, digest, "lower 32 bits of deterministic stream state",
                CounterfactualRngBaselineRecordId);
            CaptureInvariantMetric(capture, "rng", "random-stream", stableId,
                "draw-count", "draws", draws,
                path, digest, "state-advancing draws after deterministic sample schedule",
                CounterfactualRngBaselineRecordId);
        }
    }

    private static long[] ReadIntegerCsvColumn(string path, string column)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException("Missing V27 CSV evidence: " + path);
        string[] lines = File.ReadAllLines(path);
        if (lines.Length < 2)
            throw new InvalidOperationException("Empty V27 CSV evidence: " + path);
        string[] headers = lines[0].Split(',');
        int index = Array.FindIndex(headers, value =>
            string.Equals(value, column, StringComparison.Ordinal));
        if (index < 0)
            throw new InvalidOperationException($"Missing CSV column {column}: {path}");
        return lines.Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split(','))
            .Select(values => long.Parse(
                values[index], NumberStyles.Integer, CultureInfo.InvariantCulture))
            .ToArray();
    }

    private static long Percentile95(IEnumerable<long> source)
    {
        long[] values = source.OrderBy(value => value).ToArray();
        if (values.Length == 0)
            throw new InvalidOperationException("Cannot compute p95 of an empty sequence.");
        int index = checked((int)Math.Ceiling(values.Length * 0.95d) - 1);
        return values[Math.Max(0, Math.Min(values.Length - 1, index))];
    }

    private static string ParseTextKey(string line, string key)
    {
        foreach (string token in line.Split(new[] { ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = token.IndexOf('=');
            if (separator > 0
                && string.Equals(token.Substring(0, separator).Trim(), key, StringComparison.Ordinal))
                return token.Substring(separator + 1).Trim();
        }
        throw new InvalidOperationException("Missing text key " + key + " in " + line);
    }

    private static string RequireReportLine(IEnumerable<string> lines, string prefix) =>
        lines.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal))
        ?? throw new InvalidOperationException("Missing V27 evidence marker: " + prefix);

    private static long ParseKey(string line, string key)
    {
        foreach (string token in line.Split(new[] { ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = token.IndexOf('=');
            if (separator <= 0
                || !string.Equals(token.Substring(0, separator).Trim(), key, StringComparison.Ordinal))
                continue;
            if (long.TryParse(token.Substring(separator + 1), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out long value))
                return value;
        }
        throw new InvalidOperationException("Missing numeric key " + key + " in " + line);
    }

    private static void CaptureInvariantMetric(
        BalanceCaptureFactory capture,
        string domain,
        string definitionKind,
        string stableId,
        string metric,
        string unit,
        long value,
        string sourceAuthority,
        string sourceDigest,
        string formula,
        string baselineRecordId)
    {
        string token = value.ToString(CultureInfo.InvariantCulture);
        string fingerprint = HashText(stableId + "|" + metric + "|" + token + "|" + formula);
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = domain,
            DefinitionKind = definitionKind,
            StableId = stableId,
            Metric = metric,
            Unit = unit,
            Before = token,
            After = token,
            AuthoredRoundedValue = token,
            PercentDelta = "0",
            ExactFormula = formula,
            BeforeBom = "N/A",
            AfterBom = "N/A",
            BeforeDirectWu = "N/A",
            AfterDirectWu = "N/A",
            BeforeBomEwu = "N/A",
            AfterBomEwu = "N/A",
            BeforeLaborDensity = "N/A",
            AfterLaborDensity = "N/A",
            UpstreamOnlyAfter = token,
            InheritedDelta = "0",
            RawLocalDelta = "0",
            LocalQuantizationBoundaryCount = 0,
            DownstreamConsumerCount = "0",
            DependencyIds = Array.Empty<string>(),
            RootCauseIds = Array.Empty<string>(),
            AnomalyDisposition = "none",
            ReasonCode = "v27-integrated-capacity-authority",
            ReasonDetail = "Integrated survival, continuity, spatial, clutter, and RNG capacity metric.",
            SourceAuthority = sourceAuthority,
            SourcePropertyPath = metric,
            ExecutionRoute = "V27 integrated capacity audit",
            SaveAuthority = "authored authority plus durable verifier evidence",
            VerificationEvidence = sourceAuthority,
            ReviewStatus = "verified",
            ApprovalKey = string.Empty,
            DependencyFingerprint = fingerprint,
            LocalFingerprint = fingerprint,
            SourceDigest = sourceDigest,
            SemanticHash = fingerprint,
            AssetApplied = "false",
            BalanceBaselineRecordId = baselineRecordId
        });
    }

    private static string ConstructionMaterialMetric(string itemId) =>
        "construction-material-amount:"
        + BalanceCanonicalText.StableId(itemId, "construction material metric");

    private static int ResolveHistoricalIntegerBefore(
        string stableId,
        string metric,
        int current,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        if (historicalBeforeValues != null
            && historicalBeforeValues.TryGetValue(
                V27BalanceAssetApplication.BuildHistoricalBeforeKey(stableId, metric),
                out string token))
        {
            int value = int.Parse(token, NumberStyles.Integer, CultureInfo.InvariantCulture);
            if (value <= 0)
                throw new InvalidOperationException(
                    $"Historical construction amount must be positive: {stableId}:{metric}={value}.");
            return value;
        }
        return current;
    }

    private static Dictionary<string, string> FindConstructionMaterialAmountPaths(
        BuildingSO building)
    {
        SerializedObject serialized = new SerializedObject(building);
        SerializedProperty iterator = serialized.GetIterator();
        List<string> materialPaths = new List<string>();
        bool enterChildren = true;
        while (iterator.Next(enterChildren))
        {
            enterChildren = ShouldEnterSerializedChildren(iterator);
            if (string.Equals(iterator.name, "constructionMaterials", StringComparison.Ordinal)
                && iterator.isArray)
            {
                materialPaths.Add(iterator.propertyPath);
            }
        }
        string[] distinct = materialPaths.Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one constructionMaterials array on "
                + $"{AssetDatabase.GetAssetPath(building)}, found {distinct.Length}.");
        }

        SerializedProperty array = serialized.FindProperty(distinct[0])
            ?? throw new InvalidOperationException(
                $"Construction material array disappeared: {AssetDatabase.GetAssetPath(building)}.");
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < array.arraySize; index++)
        {
            SerializedProperty element = array.GetArrayElementAtIndex(index);
            SerializedProperty item = element.FindPropertyRelative("itemId")
                ?? throw new InvalidOperationException("Construction material itemId is missing.");
            SerializedProperty amount = element.FindPropertyRelative("amount")
                ?? throw new InvalidOperationException("Construction material amount is missing.");
            string itemId = BalanceCanonicalText.StableId(
                item.stringValue,
                AssetDatabase.GetAssetPath(building) + ":constructionMaterials.itemId");
            if (!result.TryAdd(itemId, amount.propertyPath))
            {
                throw new InvalidOperationException(
                    $"Duplicate construction material ID on {AssetDatabase.GetAssetPath(building)}: {itemId}.");
            }
        }
        return result;
    }

    private static int CountChangedMaterialRows(
        V27ConstructionRedistributionResult result)
    {
        Dictionary<string, int> before = result.BeforeMaterials.ToDictionary(
            value => value.ItemId,
            value => value.Amount,
            StringComparer.Ordinal);
        return result.AfterMaterials.Count(value => before[value.ItemId] != value.Amount);
    }

    private static void CaptureDismantleCycles(
        IEnumerable<BuildingSO> definitions,
        EmbeddedWorkValueSnapshot beforeValues,
        V27EmbeddedWorkValueSnapshot afterValues,
        IMaterialSalvageCalculator salvage,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests,
        IReadOnlyDictionary<string, V27ConstructionRedistributionResult>
            constructionCandidates)
    {
        foreach (BuildingSO building in definitions
                     .Where(value => value != null
                         && value.id >= 0
                         && !value.IsDeprecatedCompatibilityAsset)
                     .OrderBy(value => value.ContentDefinitionId, StringComparer.Ordinal))
        {
            string stableId = ResolveBuildingStableId(building);
            if (!constructionCandidates.TryGetValue(stableId, out
                    V27ConstructionRedistributionResult construction))
            {
                continue;
            }
            ItemAmountDefinition[] beforeMaterials = construction.BeforeMaterials
                .Where(value => value != null && value.Amount > 0)
                .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .ToArray();
            ItemAmountDefinition[] afterMaterials = construction.AfterMaterials
                .Where(value => value != null && value.Amount > 0)
                .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .ToArray();
            if (beforeMaterials.Length == 0
                || beforeMaterials.Any(value => !beforeValues.ItemWork.ContainsKey(value.ItemId))
                || afterMaterials.Any(value => !afterValues.Items.ContainsKey(value.ItemId)))
            {
                continue;
            }

            decimal beforeConstruction = construction.BeforeWu;
            decimal afterConstruction = construction.AfterWu;
            MaterialSalvageResult beforeSalvage = salvage.Calculate(
                ResolveDismantleKindForAudit(building),
                (float)beforeConstruction,
                beforeMaterials,
                100f);
            MaterialSalvageResult afterSalvage = salvage.Calculate(
                ResolveDismantleKindForAudit(building),
                (float)afterConstruction,
                afterMaterials,
                100f);
            decimal beforeDismantle = BalanceCanonicalText.DecimalFromFiniteFloat(
                beforeSalvage.RequiredWork,
                $"building:{stableId}:dismantle-work");
            decimal afterDismantle = BalanceCanonicalText.DecimalFromFiniteFloat(
                afterSalvage.RequiredWork,
                $"building:{stableId}:v27-dismantle-work");
            long beforeBom = 0L;
            long afterBom = 0L;
            foreach (ItemAmountDefinition material in beforeMaterials)
            {
                beforeBom = checked(beforeBom + V27EwuQuantizer.QuantizeInputDebit(
                    BalanceCanonicalText.DecimalFromFiniteFloat(
                        beforeValues.ItemWork[material.ItemId],
                        $"building:{stableId}:before-bom:{material.ItemId}")
                    * material.Amount).MilliEwu);
            }
            foreach (ItemAmountDefinition material in afterMaterials)
            {
                afterBom = checked(afterBom
                    + afterValues.Items[material.ItemId].AcquisitionCost.MilliEwu
                    * material.Amount);
            }

            long beforeRecovered = 0L;
            long afterRecoveredAcquisition = 0L;
            long afterRecoveredCredit = 0L;
            foreach (ItemAmountDefinition material in beforeSalvage.RecoveredMaterials)
            {
                beforeRecovered = checked(beforeRecovered + V27EwuQuantizer.QuantizeOutputCredit(
                    BalanceCanonicalText.DecimalFromFiniteFloat(
                        beforeValues.ItemWork[material.ItemId],
                        $"building:{stableId}:before-recovery:{material.ItemId}")
                    * material.Amount).MilliEwu);
            }
            foreach (ItemAmountDefinition material in afterSalvage.RecoveredMaterials)
            {
                V27ItemValue item = afterValues.Items[material.ItemId];
                afterRecoveredAcquisition = checked(
                    afterRecoveredAcquisition + item.AcquisitionCost.MilliEwu * material.Amount);
                afterRecoveredCredit = checked(
                    afterRecoveredCredit + item.RecoverableValue.MilliEwu * material.Amount);
            }

            long beforeDebit = checked(
                beforeBom
                + V27EwuQuantizer.QuantizeInputDebit(beforeConstruction).MilliEwu
                + V27EwuQuantizer.QuantizeInputDebit(beforeDismantle).MilliEwu);
            long afterDebit = checked(
                afterBom
                + V27EwuQuantizer.QuantizeInputDebit(afterConstruction).MilliEwu
                + V27EwuQuantizer.QuantizeInputDebit(afterDismantle).MilliEwu);
            long beforeMargin = checked(beforeRecovered - beforeDebit);
            long afterMargin = checked(afterRecoveredAcquisition - afterDebit);
            decimal beforeRatio = beforeDebit > 0L
                ? (decimal)beforeRecovered / beforeDebit
                : 0m;
            decimal afterRatio = afterDebit > 0L
                ? (decimal)afterRecoveredAcquisition / afterDebit
                : 0m;
            BalanceAnomalySeverity severity = afterMargin >= 0L || afterRatio >= 0.85m
                ? BalanceAnomalySeverity.Critical
                : afterRatio >= 0.80m
                    ? BalanceAnomalySeverity.Warning
                    : BalanceAnomalySeverity.None;
            string path = AssetDatabase.GetAssetPath(building);
            string sourceDigest = GetSourceDigest(path, sourceDigests);
            string[] dependencies = beforeMaterials
                .Select(value => value.ItemId)
                .Concat(afterMaterials.Select(value => value.ItemId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string dependencyFingerprint = HashText(string.Join("|", dependencies));
            string beforeToken = beforeMargin.ToString(CultureInfo.InvariantCulture);
            string afterToken = afterMargin.ToString(CultureInfo.InvariantCulture);
            string recoveredBom = FormatBom(afterSalvage.RecoveredMaterials);
            capture.Capture(new BalanceMetricCaptureRequest
            {
                Domain = "facilities",
                DefinitionKind = "building",
                StableId = stableId,
                Metric = "dismantle-rebuild-cycle-margin",
                Unit = "mEWU",
                Before = beforeToken,
                After = afterToken,
                AuthoredRoundedValue = afterToken,
                PercentDelta = Token(PercentDelta(beforeMargin, afterMargin)),
                ExactFormula = "floor(recovered material acquisition credit)-"
                    + "(ceil(BOM acquisition)+ceil(construction WU)+ceil(dismantle WU)); must be <0",
                BeforeBom = FormatBom(beforeMaterials),
                AfterBom = FormatBom(afterMaterials),
                BeforeDirectWu = Token(beforeConstruction + beforeDismantle),
                AfterDirectWu = Token(afterConstruction + afterDismantle),
                BeforeBomEwu = EwuAmount.FromMilliEwu(beforeBom).ToCanonicalEwuToken(),
                AfterBomEwu = EwuAmount.FromMilliEwu(afterBom).ToCanonicalEwuToken(),
                BeforeLaborDensity = Token(beforeRatio),
                AfterLaborDensity = Token(afterRatio),
                UpstreamOnlyAfter = checked(
                    afterRecoveredAcquisition
                    - afterBom
                    - V27EwuQuantizer.QuantizeInputDebit(beforeConstruction).MilliEwu
                    - V27EwuQuantizer.QuantizeInputDebit(beforeDismantle).MilliEwu)
                    .ToString(CultureInfo.InvariantCulture),
                InheritedDelta = checked(
                    afterRecoveredAcquisition - afterBom - (beforeRecovered - beforeBom))
                    .ToString(CultureInfo.InvariantCulture),
                RawLocalDelta = checked(
                    -V27EwuQuantizer.QuantizeInputDebit(afterConstruction).MilliEwu
                    - V27EwuQuantizer.QuantizeInputDebit(afterDismantle).MilliEwu
                    + V27EwuQuantizer.QuantizeInputDebit(beforeConstruction).MilliEwu
                    + V27EwuQuantizer.QuantizeInputDebit(beforeDismantle).MilliEwu)
                    .ToString(CultureInfo.InvariantCulture),
                LocalQuantizationBoundaryCount = dependencies.Length + 2,
                DownstreamConsumerCount = "rebuild-loop",
                DependencyIds = dependencies,
                RootCauseIds = severity == BalanceAnomalySeverity.Critical
                    ? new[] { stableId }
                    : Array.Empty<string>(),
                AnomalyDisposition = severity == BalanceAnomalySeverity.Critical
                    ? "local-critical"
                    : severity == BalanceAnomalySeverity.Warning ? "warning" : "none",
                ReasonCode = "v27-dismantle-rebuild-thermodynamic-loss",
                ReasonDetail = "Recovered=" + recoveredBom
                    + "; acquisition-offset ratio=" + Token(afterRatio)
                    + "; SCC credit uses stricter output recoverable value="
                    + afterRecoveredCredit.ToString(CultureInfo.InvariantCulture) + "mEWU.",
                SourceAuthority = path,
                SourcePropertyPath = "construction materials + derived dismantle policy",
                ExecutionRoute = "V27BalanceWorkCalculator->WorkAmountSystem.Dismantle->V23MaterialSalvageCalculator->rebuild",
                SaveAuthority = "BuildingSO + WorkOrderRuntime",
                VerificationEvidence = "V27 dismantle SCC audit; PlayMode evidence pending",
                ReviewStatus = severity == BalanceAnomalySeverity.Critical ? "pending" : "review",
                ApprovalKey = string.Empty,
                DependencyFingerprint = dependencyFingerprint,
                LocalFingerprint = HashText(
                    stableId + "|" + FormatBom(beforeMaterials) + "|"
                    + FormatBom(afterMaterials) + "|" + recoveredBom),
                SourceDigest = sourceDigest,
                SemanticHash = HashText(stableId + "|dismantle-rebuild-cycle-margin|" + afterToken),
                AssetApplied = "false",
                BalanceBaselineRecordId = ResolveBaselineRecordId(stableId)
            });
            capture.Capture(new BalanceMetricCaptureRequest
            {
                Domain = "facilities",
                DefinitionKind = "building",
                StableId = stableId,
                Metric = "dismantle-wu:duration-preserving",
                Unit = "WU",
                Before = Token(beforeDismantle),
                After = Token(afterDismantle),
                AuthoredRoundedValue = Token(afterDismantle),
                PercentDelta = Token(PercentDelta(beforeDismantle, afterDismantle)),
                ExactFormula = "V23 salvage policy applied to approved construction WU and physical BOM",
                BeforeBom = FormatBom(beforeSalvage.RecoveredMaterials),
                AfterBom = recoveredBom,
                BeforeDirectWu = Token(beforeDismantle),
                AfterDirectWu = Token(afterDismantle),
                BeforeBomEwu = EwuAmount.FromMilliEwu(beforeRecovered).ToCanonicalEwuToken(),
                AfterBomEwu = EwuAmount.FromMilliEwu(afterRecoveredAcquisition).ToCanonicalEwuToken(),
                BeforeLaborDensity = "N/A",
                AfterLaborDensity = "N/A",
                UpstreamOnlyAfter = Token(beforeDismantle),
                InheritedDelta = "0",
                RawLocalDelta = Token(afterDismantle - beforeDismantle),
                LocalQuantizationBoundaryCount = 1,
                DownstreamConsumerCount = "dismantle-runtime",
                DependencyIds = dependencies,
                RootCauseIds = Array.Empty<string>(),
                AnomalyDisposition = "warning",
                ReasonCode = "v27-dismantle-work-duration-preserving",
                ReasonDetail = "Dismantle work and recovery are derived from the approved authored construction WU/BOM pair.",
                SourceAuthority = "Assets/Scripts/Services/Economy/V27BalanceWorkCalculator.cs",
                SourcePropertyPath = "V27BalanceWorkCalculator.CalculateConstruction->V23MaterialSalvageCalculator.Calculate.requiredWork",
                ExecutionRoute = "WorkAmountSystem->IMaterialSalvageCalculator",
                SaveAuthority = "derived runtime policy",
                VerificationEvidence = "V27 dismantle audit + V27_VERTICAL_SLICE_DISMANTLE_REBUILD_NEGATIVE",
                ReviewStatus = "implemented",
                ApprovalKey = string.Empty,
                DependencyFingerprint = dependencyFingerprint,
                LocalFingerprint = HashText(stableId + "|dismantle-wu|" + Token(beforeDismantle)),
                SourceDigest = GetSourceDigest(
                    "Assets/Scripts/Services/Economy/V27BalanceWorkCalculator.cs",
                    sourceDigests),
                SemanticHash = HashText(stableId + "|dismantle-wu|" + Token(afterDismantle)),
                AssetApplied = "false",
                BalanceBaselineRecordId = ResolveBaselineRecordId(stableId)
            });
            if (severity != BalanceAnomalySeverity.None)
            {
                anomalies.Add(BalanceAnomalyNode.Capture(
                    stableId,
                    "dismantle-rebuild-cycle-margin",
                    severity,
                    severity == BalanceAnomalySeverity.Critical
                        ? BalanceAnomalyDisposition.LocalCritical
                        : BalanceAnomalyDisposition.None,
                    "v27-dismantle-rebuild-thermodynamic-loss",
                    dependencies));
            }
        }
    }

    private static IEnumerable<BalanceTransform> BuildDismantleTransforms(
        IEnumerable<BuildingSO> definitions,
        V27EmbeddedWorkValueSnapshot afterValues,
        IMaterialSalvageCalculator salvage,
        IReadOnlyDictionary<string, V27ConstructionRedistributionResult>
            constructionCandidates)
    {
        foreach (BuildingSO building in definitions
                     .Where(value => value != null
                         && value.id >= 0
                         && !value.IsDeprecatedCompatibilityAsset)
                     .OrderBy(value => value.ContentDefinitionId, StringComparer.Ordinal))
        {
            string stableId = ResolveBuildingStableId(building);
            if (!constructionCandidates.TryGetValue(
                    stableId,
                    out V27ConstructionRedistributionResult constructionResult))
            {
                continue;
            }
            ItemAmountDefinition[] materials = constructionResult.AfterMaterials
                .Where(value => value != null && value.Amount > 0)
                .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .ToArray();
            if (materials.Length == 0
                || materials.Any(value => !afterValues.Items.ContainsKey(value.ItemId)))
            {
                continue;
            }
            decimal construction = constructionResult.AfterWu;
            MaterialSalvageResult recovered = salvage.Calculate(
                ResolveDismantleKindForAudit(building),
                (float)construction,
                materials,
                100f);
            if (recovered.RecoveredMaterials.Count == 0)
            {
                // A zero-output dismantle is a strict sink. It cannot participate in
                // a positive cycle and BalanceTransform intentionally requires at
                // least one output node. The exhaustive ledger still records it.
                continue;
            }
            decimal dismantle = BalanceCanonicalText.DecimalFromFiniteFloat(
                recovered.RequiredWork,
                $"building:{building.id}:scc-dismantle");
            long debit = checked(
                materials.Sum(value => checked(
                    afterValues.Items[value.ItemId].AcquisitionCost.MilliEwu * value.Amount))
                + V27EwuQuantizer.QuantizeInputDebit(construction).MilliEwu
                + V27EwuQuantizer.QuantizeInputDebit(dismantle).MilliEwu);
            long credit = recovered.RecoveredMaterials.Sum(value => checked(
                afterValues.Items[value.ItemId].RecoverableValue.MilliEwu * value.Amount));
            yield return BalanceTransform.Capture(
                "dismantle:" + stableId,
                materials.Select(value => value.ItemId),
                recovered.RecoveredMaterials.Select(value => value.ItemId),
                debit,
                credit);
        }
    }

    private static DismantleTargetKind ResolveDismantleKindForAudit(BuildingSO building)
    {
        ConstructionBalanceClass balanceClass =
            V23BalanceWorkCalculator.ResolveConstructionClass(building);
        return balanceClass == ConstructionBalanceClass.Arcane
            ? DismantleTargetKind.ArcaneFacility
            : balanceClass is ConstructionBalanceClass.Precision
                or ConstructionBalanceClass.Medical
                or ConstructionBalanceClass.Industrial
                ? DismantleTargetKind.PrecisionIndustrialFacility
                : DismantleTargetKind.GeneralFacility;
    }

    private static void CaptureBuildingCandidate(
        BalanceCaptureFactory capture,
        BuildingSO building,
        string stableId,
        string path,
        string sourceDigest,
        string[] dependencies,
        string metric,
        decimal beforeWu,
        decimal afterWu,
        string beforeBom,
        string afterBom,
        decimal beforeBomEwu,
        decimal afterBomEwu,
        decimal beforeDensity,
        decimal afterDensity,
        BalanceAnomalySeverity severity,
        string reasonCode,
        string reason,
        bool patchable,
        string sourcePropertyPath,
        string reviewStatus,
        bool assetApplied = false,
        bool reviewOnlyCandidate = false)
    {
        string fingerprint = HashText(
            stableId + "|" + beforeBom + "|" + afterBom + "|"
            + building.width + "x" + building.height);
        string dependencyFingerprint = HashText(string.Join("|", dependencies));
        string afterToken = Token(afterWu);
        string approvalSourceDigest = patchable
            ? GetApprovalSourceDigest(
                path,
                sourcePropertyPath.Substring(
                    sourcePropertyPath.LastIndexOf('.') + 1))
            : sourceDigest;
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = "facilities",
            DefinitionKind = "building",
            StableId = stableId,
            Metric = metric,
            Unit = "WU",
            Before = Token(beforeWu),
            After = afterToken,
            AuthoredRoundedValue = Token(afterWu),
            PercentDelta = Token(PercentDelta(beforeWu, afterWu)),
            ExactFormula = reason,
            BeforeBom = beforeBom,
            AfterBom = afterBom,
            BeforeDirectWu = Token(beforeWu),
            AfterDirectWu = Token(afterWu),
            BeforeBomEwu = Token(beforeBomEwu),
            AfterBomEwu = Token(afterBomEwu),
            BeforeLaborDensity = Token(beforeDensity),
            AfterLaborDensity = Token(afterDensity),
            UpstreamOnlyAfter = Token(beforeWu),
            InheritedDelta = "0",
            RawLocalDelta = Token(afterWu - beforeWu),
            LocalQuantizationBoundaryCount = dependencies.Length + 1,
            DownstreamConsumerCount = reviewOnlyCandidate
                ? "review-only"
                : "facility-runtime",
            DependencyIds = dependencies,
            RootCauseIds = dependencies,
            AnomalyDisposition = severity == BalanceAnomalySeverity.Critical
                    ? "local-critical"
                    : severity == BalanceAnomalySeverity.Warning ? "warning" : "none",
            ReasonCode = reasonCode,
            ReasonDetail = reason,
            SourceAuthority = path,
            SourcePropertyPath = sourcePropertyPath,
            ExecutionRoute = reviewOnlyCandidate
                ? "review-only:V27ConstructionRedistributionPolicy->explicit promotion transaction"
                : "BuildingSO->ConstructionSite->AIWork",
            SaveAuthority = reviewOnlyCandidate
                ? "derived optimizer proposal + explicit review authority"
                : "BuildingSO",
            VerificationEvidence = reviewOnlyCandidate
                ? "V27 previous-applied/recalibration-candidate separation audit"
                : "V27 facility candidate audit",
            ReviewStatus = reviewStatus,
            ApprovalKey = patchable && beforeWu != afterWu
                ? BuildApprovalKey(stableId, metric, afterToken, dependencyFingerprint,
                    approvalSourceDigest, reasonCode, ResolveLaborBaselineRecordId(stableId))
                : string.Empty,
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = fingerprint,
            SourceDigest = approvalSourceDigest,
            SemanticHash = HashText(
                stableId + "|" + metric + "|" + Token(afterWu) + "|" + afterBom),
            AssetApplied = assetApplied ? "true" : "false",
            BalanceBaselineRecordId = ResolveLaborBaselineRecordId(stableId)
        });
    }

    private static void CaptureBuildingMaterialAmount(
        BalanceCaptureFactory capture,
        string stableId,
        string path,
        string sourceDigest,
        string[] dependencies,
        string itemId,
        string metric,
        int beforeAmount,
        int afterAmount,
        string beforeBom,
        string afterBom,
        decimal beforeBomEwu,
        decimal afterBomEwu,
        string sourcePropertyPath,
        BalanceAnomalySeverity severity,
        string reasonCode,
        string reasonDetail,
        string reviewStatus,
        bool approvalEligible,
        bool assetApplied,
        bool reviewOnlyCandidate,
        int historicalBaselineAmount)
    {
        string dependencyFingerprint = HashText(string.Join("|", dependencies));
        string approvalSourceDigest = approvalEligible
            ? GetConstructionMaterialApprovalSourceDigest(path, itemId)
            : sourceDigest;
        string afterToken = afterAmount.ToString(CultureInfo.InvariantCulture);
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = "facilities",
            DefinitionKind = "building-material",
            StableId = stableId,
            Metric = metric,
            Unit = "item",
            Before = beforeAmount.ToString(CultureInfo.InvariantCulture),
            After = afterToken,
            AuthoredRoundedValue = afterToken,
            PercentDelta = Token(PercentDelta(beforeAmount, afterAmount)),
            ExactFormula = reviewOnlyCandidate
                ? "candidate in [historicalBaseline="
                    + historicalBaselineAmount.ToString(CultureInfo.InvariantCulture)
                    + ",ceil(historicalBaseline*1.5)="
                    + ((historicalBaselineAmount * 3 + 1) / 2).ToString(
                        CultureInfo.InvariantCulture)
                    + "]; currentApplied="
                    + beforeAmount.ToString(CultureInfo.InvariantCulture)
                    + "; existing item IDs only"
                : "integer amount in [Before,ceil(Before*1.5)]; existing item IDs only",
            BeforeBom = beforeBom,
            AfterBom = afterBom,
            BeforeDirectWu = "N/A",
            AfterDirectWu = "N/A",
            BeforeBomEwu = Token(beforeBomEwu),
            AfterBomEwu = Token(afterBomEwu),
            BeforeLaborDensity = "N/A",
            AfterLaborDensity = "N/A",
            UpstreamOnlyAfter = beforeAmount.ToString(CultureInfo.InvariantCulture),
            InheritedDelta = "0",
            RawLocalDelta = (afterAmount - beforeAmount).ToString(CultureInfo.InvariantCulture),
            LocalQuantizationBoundaryCount = 1,
            DownstreamConsumerCount = reviewOnlyCandidate
                ? "review-only"
                : "construction-runtime",
            DependencyIds = dependencies,
            RootCauseIds = Array.Empty<string>(),
            AnomalyDisposition = severity == BalanceAnomalySeverity.Critical
                ? "local-critical"
                : severity == BalanceAnomalySeverity.Warning ? "warning" : "none",
            ReasonCode = reasonCode,
            ReasonDetail = reasonDetail,
            SourceAuthority = path,
            SourcePropertyPath = sourcePropertyPath,
            ExecutionRoute = reviewOnlyCandidate
                ? "review-only:V27ConstructionRedistributionPolicy->explicit promotion transaction"
                : "BuildingSO->WorkAmountSystem.RequestMissingMaterials->AIHaul->ConstructionSite",
            SaveAuthority = reviewOnlyCandidate
                ? "derived optimizer proposal + explicit review authority"
                : "BuildingSO + physical world-item runtime",
            VerificationEvidence = reviewOnlyCandidate
                ? "V27 previous-applied/recalibration-candidate separation audit"
                : "V27 construction redistribution + physical logistics PlayMode",
            ReviewStatus = reviewStatus,
            ApprovalKey = approvalEligible && beforeAmount != afterAmount
                ? BuildApprovalKey(
                    stableId,
                    metric,
                    afterToken,
                    dependencyFingerprint,
                    approvalSourceDigest,
                    reasonCode,
                    ResolveLaborBaselineRecordId(stableId))
                : string.Empty,
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = HashText(
                stableId + "|" + metric + "|" + itemId + "|"
                + beforeAmount + "|" + historicalBaselineAmount),
            SourceDigest = approvalSourceDigest,
            SemanticHash = HashText(stableId + "|" + metric + "|" + afterToken),
            AssetApplied = assetApplied ? "true" : "false",
            BalanceBaselineRecordId = ResolveLaborBaselineRecordId(stableId)
        });
    }

    private static void AddPipelineReadinessAnomalies(ICollection<BalanceAnomalyNode> anomalies)
    {
        string analyzerDll = ProjectAbsolutePath(
            "Assets/Analyzers/DungeonStory.BalanceAnalyzers.dll");
        if (!File.Exists(analyzerDll))
        {
            anomalies.Add(BalanceAnomalyNode.Capture(
                "architecture:v27-ledger",
                "analyzer-deployment",
                BalanceAnomalySeverity.Critical,
                BalanceAnomalyDisposition.RootCritical,
                "analyzer-binary-missing",
                Array.Empty<string>()));
        }
    }

    private static void PromoteDependencyRoots(
        FrozenBalanceLedger ledger,
        ICollection<BalanceAnomalyNode> anomalies,
        IReadOnlyCollection<string> approvedKeys)
    {
        HashSet<string> approved = new HashSet<string>(
            approvedKeys ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        HashSet<string> existingRoots = anomalies
            .Where(value => value.EmitsCiAnnotation)
            .Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        string[] referencedRoots = anomalies
            .Where(value => value.Severity == BalanceAnomalySeverity.Critical
                && !value.EmitsCiAnnotation)
            .SelectMany(value => value.RootCauseIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        foreach (string rootId in referencedRoots)
        {
            if (existingRoots.Contains(rootId))
                continue;
            CanonicalBalanceMetricRecord[] causalRows = ledger.Records
                .Where(value => string.Equals(value.StableId, rootId, StringComparison.Ordinal)
                    && value.ApprovalKey.Length > 0
                    && DependencyRootMetricPriority(value.Metric) < int.MaxValue)
                .OrderBy(value => approved.Contains(value.ApprovalKey) ? 0 : 1)
                .ThenBy(value => DependencyRootMetricPriority(value.Metric))
                .ThenBy(value => value.Metric, StringComparer.Ordinal)
                .ToArray();
            if (causalRows.Length == 0)
                throw new InvalidOperationException(
                    "Collapsed Critical references a root with no causal approvable ledger row: "
                    + rootId);
            CanonicalBalanceMetricRecord record = causalRows[0];
            anomalies.Add(BalanceAnomalyNode.Capture(
                rootId,
                record.Metric,
                BalanceAnomalySeverity.Critical,
                BalanceAnomalyDisposition.RootCritical,
                record.ReasonCode,
                Array.Empty<string>()));
            existingRoots.Add(rootId);
        }
    }

    private static List<BalanceAnomalyNode> ApplyApprovalDispositions(
        FrozenBalanceLedger ledger,
        IReadOnlyCollection<BalanceAnomalyNode> anomalies,
        IReadOnlyCollection<string> approvedKeys)
    {
        if (approvedKeys == null || approvedKeys.Count == 0)
            return anomalies.ToList();
        HashSet<string> approved = new HashSet<string>(approvedKeys, StringComparer.Ordinal);
        HashSet<string> approvedRows = ledger.Records
            .Where(value => value.ApprovalKey.Length > 0 && approved.Contains(value.ApprovalKey))
            .Select(value => value.StableId + "\u001f" + value.Metric)
            .ToHashSet(StringComparer.Ordinal);
        List<BalanceAnomalyNode> result = new List<BalanceAnomalyNode>(anomalies.Count);
        foreach (BalanceAnomalyNode anomaly in anomalies)
        {
            string key = anomaly.StableId + "\u001f" + anomaly.Metric;
            result.Add(anomaly.EmitsCiAnnotation && approvedRows.Contains(key)
                ? BalanceAnomalyNode.Capture(
                    anomaly.StableId,
                    anomaly.Metric,
                    anomaly.Severity,
                    BalanceAnomalyDisposition.Approved,
                    anomaly.ReasonCode,
                    anomaly.RootCauseIds)
                : anomaly);
        }
        return result;
    }

    private static void WriteMarkdown(
        Stream stream,
        FrozenBalanceLedger ledger,
        IReadOnlyList<BalanceAnomalyNode> anomalies,
        BalanceSccAuditResult scc,
        IReadOnlyCollection<string> failures)
    {
        using StreamWriter writer = NewLfWriter(stream);
        writer.Write("# V27 Balance Before/After Summary\n\n");
        writer.Write("This file is generated deterministically from current Unity authority. ");
        writer.Write("The exhaustive machine ledger is `Artifacts/QA/v27-balance-before-after.csv`.\n\n");
        writer.Write("- Schema: v27.1\n");
        writer.Write("- Rows: "); writer.Write(ledger.Count); writer.Write('\n');
        writer.Write("- Unresolved root/local Critical nodes: ");
        writer.Write(anomalies.Count(value => value.EmitsCiAnnotation));
        writer.Write('\n');
        writer.Write("- Collapsed Critical descendants: ");
        writer.Write(anomalies.Count(value => value.Severity == BalanceAnomalySeverity.Critical
            && !value.EmitsCiAnnotation));
        writer.Write('\n');
        writer.Write("- SCCs: "); writer.Write(scc.Components.Count); writer.Write('\n');
        writer.Write("- Integrity failures: "); writer.Write(failures.Count); writer.Write("\n\n");
        writer.Write("| Domain | Kind | Stable ID | Metric | Before | After | Status |\n");
        writer.Write("|---|---|---|---|---:|---:|---|\n");
        foreach (CanonicalBalanceMetricRecord record in ledger.Records)
        {
            writer.Write('|');
            WriteMarkdownCell(writer, record.Domain); writer.Write('|');
            WriteMarkdownCell(writer, record.DefinitionKind); writer.Write('|');
            WriteMarkdownCell(writer, record.StableId); writer.Write('|');
            WriteMarkdownCell(writer, record.Metric); writer.Write('|');
            WriteMarkdownCell(writer, record.Before); writer.Write('|');
            WriteMarkdownCell(writer, record.After); writer.Write('|');
            WriteMarkdownCell(writer, record.AnomalyDisposition); writer.Write("|\n");
        }
        writer.Flush();
    }

    private static void WriteAudit(
        Stream stream,
        FrozenBalanceLedger ledger,
        IReadOnlyList<BalanceAnomalyNode> anomalies,
        BalanceSccAuditResult scc,
        IReadOnlyList<string> failures)
    {
        using StreamWriter writer = NewLfWriter(stream);
        int critical = anomalies.Count(value => value.EmitsCiAnnotation);
        int collapsed = anomalies.Count(value => value.Severity == BalanceAnomalySeverity.Critical
            && !value.EmitsCiAnnotation);
        int approved = anomalies.Count(value => value.Disposition == BalanceAnomalyDisposition.Approved);
        if (critical > 0)
            writer.Write("[CRITICAL REVIEW REQUIRED]\n\n");
        writer.Write(failures.Count > 0
            ? "RESULT=FAIL"
            : critical > 0 ? "RESULT=REVIEW_REQUIRED" : "RESULT=PASS");
        writer.Write("; rows="); writer.Write(ledger.Count);
        writer.Write("; critical="); writer.Write(critical);
        writer.Write("; collapsed="); writer.Write(collapsed);
        writer.Write("; approved="); writer.Write(approved);
        writer.Write("; scc="); writer.Write(scc.Components.Count);
        writer.Write("; minimumMarginMilliEwu="); writer.Write(scc.MinimumMarginMilliEwu);
        writer.Write("; integrityFailures="); writer.Write(failures.Count);
        writer.Write("\n");
        foreach (string failure in failures)
        {
            writer.Write("INTEGRITY_FAIL "); writer.Write(failure); writer.Write('\n');
        }
        foreach (IGrouping<string, BalanceAnomalyNode> group in anomalies
                     .Where(value => value.EmitsCiAnnotation)
                     .GroupBy(value => value.StableId, StringComparer.Ordinal)
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            writer.Write("\nROOT "); writer.Write(group.Key); writer.Write('\n');
            foreach (BalanceAnomalyNode node in group.OrderBy(value => value.Metric, StringComparer.Ordinal))
            {
                writer.Write("  "); writer.Write(AuditDispositionToken(node.Disposition));
                writer.Write(" metric="); writer.Write(node.Metric);
                writer.Write(" reason="); writer.Write(node.ReasonCode);
                CanonicalBalanceMetricRecord approvalRow = ledger.Records.FirstOrDefault(value =>
                    string.Equals(value.StableId, node.StableId, StringComparison.Ordinal)
                    && string.Equals(value.Metric, node.Metric, StringComparison.Ordinal));
                writer.Write(" approvalKey="); writer.Write(approvalRow?.ApprovalKey ?? string.Empty);
                writer.Write(" roots=");
                for (int index = 0; index < node.RootCauseIds.Count; index++)
                {
                    if (index != 0) writer.Write('|');
                    writer.Write(node.RootCauseIds[index]);
                }
                writer.Write('\n');
            }
            int collapsedChildren = anomalies.Count(value =>
                value.Severity == BalanceAnomalySeverity.Critical
                && !value.EmitsCiAnnotation
                && value.RootCauseIds.Contains(group.Key, StringComparer.Ordinal));
            writer.Write("  COLLAPSED inherited-or-rounding: ");
            writer.Write(collapsedChildren);
            writer.Write('\n');
        }
        foreach (BalanceAnomalyNode node in anomalies
                     .Where(value => value.Disposition == BalanceAnomalyDisposition.Approved)
                     .OrderBy(value => value.StableId, StringComparer.Ordinal)
                     .ThenBy(value => value.Metric, StringComparer.Ordinal))
        {
            writer.Write("\nAPPROVED "); writer.Write(node.StableId);
            writer.Write(" metric="); writer.Write(node.Metric);
            writer.Write(" reason="); writer.Write(node.ReasonCode);
            writer.Write('\n');
        }
        writer.Flush();
    }

    private static void WriteSourceInventory(
        Stream stream,
        IReadOnlyDictionary<string, string> sourceDigests)
    {
        using StreamWriter writer = NewLfWriter(stream);
        writer.Write("{\n");
        WriteJsonProperty(writer, "schemaVersion", "v27.source.v2", true);
        writer.Write("  \"entries\": [\n");
        KeyValuePair<string, string>[] ordered = sourceDigests
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < ordered.Length; index++)
        {
            KeyValuePair<string, string> entry = ordered[index];
            writer.Write("    {\"path\":");
            V27BalanceJsonSerializer.WriteJsonString(writer, entry.Key);
            writer.Write(",\"sha256\":");
            V27BalanceJsonSerializer.WriteJsonString(writer, entry.Value);
            writer.Write(index + 1 < ordered.Length ? "},\n" : "}\n");
        }
        writer.Write("  ]\n");
        writer.Write("}\n");
        writer.Flush();
    }

    private static string[] CaptureBaselineRecordIds() => new[]
    {
        BaselineRecordId,
        EvidenceBaselineRecordId,
        VerticalSliceBaselineRecordId,
        SurvivalOutputBaselineRecordId,
        MarketBaselineRecordId,
        LaborFacilityBaselineRecordId,
        ResearchScheduleBaselineRecordId,
        DungeonExpansionBaselineRecordId,
        DungeonExpansionWidthBaselineRecordId,
        LaborAuthorityBaselineRecordId,
        LaborMatrixBaselineRecordId,
        EquipmentReadinessBaselineRecordId,
        CombatOutcomeBaselineRecordId,
        DailyRoutineEvidenceBaselineRecordId,
        ServiceContinuityBaselineRecordId,
        PrimitiveFallbackBaselineRecordId,
        SharedAccessBaselineRecordId,
        FloorClutterBaselineRecordId,
        OverflowContainmentBaselineRecordId,
        CounterfactualRngBaselineRecordId,
        PairedRunBaselineRecordId,
        PopulationCapacityBaselineRecordId,
        SixAdultClosedLoopBaselineRecordId,
        IntegratedCapacityValidationBaselineRecordId,
        OutputContainmentBaselineRecordId,
        MultiOutputEconomicAllocationBaselineRecordId
    };

    private static void WriteManifest(
        Stream stream,
        BalanceArtifactManifest manifest,
        string csvHash,
        string markdownHash,
        string auditHash,
        string anomalyHash,
        string sourceInventoryHash,
        string approvalHash,
        string assetPatchDigest)
    {
        using StreamWriter writer = NewLfWriter(stream);
        writer.Write("{\n");
        WriteJsonProperty(writer, "schemaVersion", manifest.SchemaVersion, true);
        WriteJsonProperty(writer, "generatorVersion", manifest.GeneratorVersion, true);
        WriteJsonProperty(writer, "sourceDigest", manifest.Authority.SourceDigest, true);
        WriteJsonProperty(writer, "sourceInventoryByteHash", sourceInventoryHash, true);
        WriteJsonNumber(writer, "sourceCount", manifest.Authority.SourceCount, true);
        WriteJsonNumber(writer, "rowCount", manifest.Authority.Ledger.Count, true);
        WriteJsonProperty(writer, "csvByteHash", csvHash, true);
        WriteJsonProperty(writer, "markdownByteHash", markdownHash, true);
        WriteJsonProperty(writer, "auditByteHash", auditHash, true);
        WriteJsonProperty(writer, "anomalyGraphByteHash", anomalyHash, true);
        string economyEvidence = ProjectAbsolutePath(
            V27BalanceEconomySimulationDebugScenarios.ReportPath);
        string verticalSliceEvidence = ProjectAbsolutePath(
            V27BalanceVerticalSlicePlayModeVerifier.ReportPath);
        string rollbackEvidence = ProjectAbsolutePath(
            V27BalanceAssetRollbackDebugScenarios.ReportPath);
        string marketEvidence = ProjectAbsolutePath(
            V27BalanceMarketDebugScenarios.ReportPath);
        string laborFacilityEvidence = ProjectAbsolutePath(
            V27BalanceLaborFacilityDebugScenarios.ReportPath);
        string laborMatrixEvidence = ProjectAbsolutePath(
            V27LaborAuthorityMatrixDebugScenarios.ReportPath);
        string combatOutcomeEvidence = ProjectAbsolutePath(
            CombatOutcomeBalanceCalibrationScenario.FinalCheckpointAggregateReportPath);
        string wholeGameCoverageEvidence = ProjectAbsolutePath(
            V27BalanceWholeGameCoverageDebugScenarios.ReportPath);
        string ledgerContractEvidence = ProjectAbsolutePath(
            V27BalanceLedgerDebugScenarios.ReportPath);
        string equipmentReadinessEvidence = ProjectAbsolutePath(
            "Artifacts/QA/v26-equipment-readiness-throughput.md");
        string dailyRoutine157181Evidence = ProjectAbsolutePath(
            "Artifacts/QA/phase157-daily-routine-wu-seed-157181.txt");
        string dailyRoutine157182Evidence = ProjectAbsolutePath(
            "Artifacts/QA/phase157-daily-routine-wu-seed-157182.txt");
        string dailyRoutine157183Evidence = ProjectAbsolutePath(
            "Artifacts/QA/phase157-daily-routine-wu-seed-157183.txt");
        string expansionEditModeEvidence = ProjectAbsolutePath(
            "Artifacts/QA/v27-balance-expansion-editmode.txt");
        string expansionPlayModeEvidence = ProjectAbsolutePath(
            "Artifacts/QA/v27-balance-expansion-playmode.txt");
        string expansionLayoutEvidence = ProjectAbsolutePath(
            "Artifacts/QA/v27-balance-layout-256-seed.txt");
        string stagePortfolioEvidence = ProjectAbsolutePath(
            "Artifacts/QA/v27-balance-stage-portfolios.csv");
        string serviceContinuityEvidence = ProjectAbsolutePath(
            "Artifacts/QA/v27-balance-service-continuity.csv");
        string serviceContinuityPlayModeEvidence = ProjectAbsolutePath(
            V27ServiceContinuityEvidenceDebugScenarios.ReportPath);
        string populationStagePlayModeEvidence = ProjectAbsolutePath(
            PrimitiveStartSurvivalPlayModeVerifier.PopulationStageReportPath);
        string spatialCapacityEvidence = ProjectAbsolutePath(
            "Artifacts/QA/v27-balance-spatial-capacity.csv");
        string floorClutterEvidence = ProjectAbsolutePath(
            V27PairedClutterPlayModeVerifier.ClutterCsvPath);
        string pairedRunEvidence = ProjectAbsolutePath(
            V27PairedClutterPlayModeVerifier.PairedCsvPath);
        string pairedRunReportEvidence = ProjectAbsolutePath(
            V27PairedClutterPlayModeVerifier.ReportPath);
        string randomStreamEvidence = ProjectAbsolutePath(
            V27RandomStreamManifestDebugScenarios.ReportPath);
        string sharedCongestionEvidence = ProjectAbsolutePath(
            "Artifacts/QA/v27-balance-shared-cell-congestion.txt");
        string sixAdultEvidence = ProjectAbsolutePath(
            V27SixAdultSurvivalLoopDebugScenarios.ReportPath);
        string expansionTierEvidence = ProjectAbsolutePath(
            "Artifacts/QA/v27-balance-expansion-tiers.txt");
        string finalAcceptanceEvidence = ProjectAbsolutePath(
            DungeonStoryFinalAcceptanceRunner.ReportRelativePath);
        WriteJsonProperty(writer, "economy256EvidenceHash",
            File.Exists(economyEvidence)
                ? HashFile(economyEvidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "verticalSliceFullLoopEvidenceHash",
            File.Exists(verticalSliceEvidence)
                ? HashFile(verticalSliceEvidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "assetRollbackEvidenceHash",
            File.Exists(rollbackEvidence)
                ? HashFile(rollbackEvidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "marketAuthorityEvidenceHash",
            File.Exists(marketEvidence)
                ? HashFile(marketEvidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "laborFacilityAuthorityEvidenceHash",
            File.Exists(laborFacilityEvidence)
                ? HashFile(laborFacilityEvidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "laborAuthorityMatrixEvidenceHash",
            File.Exists(laborMatrixEvidence)
                ? HashFile(laborMatrixEvidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "combatOutcome1000SeedEvidenceHash",
            File.Exists(combatOutcomeEvidence)
                ? HashFile(combatOutcomeEvidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "wholeGameCoverageEvidenceHash",
            File.Exists(wholeGameCoverageEvidence)
                ? HashFile(wholeGameCoverageEvidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "ledgerContractEvidenceHash",
            File.Exists(ledgerContractEvidence)
                ? HashFile(ledgerContractEvidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "equipmentReadinessEvidenceHash",
            File.Exists(equipmentReadinessEvidence)
                ? HashFile(equipmentReadinessEvidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "dailyRoutine157181EvidenceHash",
            File.Exists(dailyRoutine157181Evidence)
                ? HashFile(dailyRoutine157181Evidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "dailyRoutine157182EvidenceHash",
            File.Exists(dailyRoutine157182Evidence)
                ? HashFile(dailyRoutine157182Evidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "dailyRoutine157183EvidenceHash",
            File.Exists(dailyRoutine157183Evidence)
                ? HashFile(dailyRoutine157183Evidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "dungeonExpansionEditModeEvidenceHash",
            File.Exists(expansionEditModeEvidence)
                ? HashFile(expansionEditModeEvidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "dungeonExpansionPlayModeEvidenceHash",
            File.Exists(expansionPlayModeEvidence)
                ? HashFile(expansionPlayModeEvidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "dungeonExpansionLayout1536EvidenceHash",
            File.Exists(expansionLayoutEvidence)
                ? HashFile(expansionLayoutEvidence)
                : HashText(string.Empty), true);
        WriteEvidenceHash(writer, "stagePortfolioEvidenceHash", stagePortfolioEvidence);
        WriteEvidenceHash(writer, "serviceContinuityCatalogEvidenceHash", serviceContinuityEvidence);
        WriteEvidenceHash(writer, "serviceContinuityLiveEvidenceHash", serviceContinuityPlayModeEvidence);
        WriteEvidenceHash(writer, "populationStageLiveEvidenceHash", populationStagePlayModeEvidence);
        WriteEvidenceHash(writer, "assetBackedSpatialCapacityEvidenceHash", spatialCapacityEvidence);
        WriteEvidenceHash(writer, "floorClutterEvidenceHash", floorClutterEvidence);
        WriteEvidenceHash(writer, "pairedRunWindowEvidenceHash", pairedRunEvidence);
        WriteEvidenceHash(writer, "pairedRunAggregateEvidenceHash", pairedRunReportEvidence);
        WriteEvidenceHash(writer, "randomStreamManifestEvidenceHash", randomStreamEvidence);
        WriteEvidenceHash(writer, "sharedCellCongestionEvidenceHash", sharedCongestionEvidence);
        WriteEvidenceHash(writer, "sixAdultClosedLoopEvidenceHash", sixAdultEvidence);
        WriteEvidenceHash(writer, "expansionTierCapacityEvidenceHash", expansionTierEvidence);
        WriteJsonProperty(writer, "finalAcceptanceEvidenceHash",
            File.Exists(finalAcceptanceEvidence)
                ? HashFile(finalAcceptanceEvidence)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "approvalDigest", approvalHash, true);
        WriteJsonProperty(writer, "assetPatchDigest", assetPatchDigest, true);
        string analyzerSource = ProjectAbsolutePath(
            "tools/DungeonStory.BalanceAnalyzers/DungeonStoryBalanceAnalyzer.cs");
        string analyzerDll = ProjectAbsolutePath("Assets/Analyzers/DungeonStory.BalanceAnalyzers.dll");
        WriteJsonProperty(writer, "analyzerSourceHash",
            File.Exists(analyzerSource)
                ? HashCanonicalSourceFile(analyzerSource)
                : HashText(string.Empty), true);
        WriteJsonProperty(writer, "analyzerDllHash",
            File.Exists(analyzerDll) ? HashFile(analyzerDll) : HashText(string.Empty), true);
        WriteJsonNumber(writer, "criticalCount", manifest.CriticalCount, true);
        WriteJsonNumber(writer, "collapsedCriticalCount",
            manifest.CollapsedCriticalCount, true);
        WriteJsonNumber(writer, "approvedCount", manifest.ApprovedCount, true);
        WriteJsonNumber(writer, "sccCount", manifest.SccCount, true);
        WriteJsonNumber(writer, "integrityFailureCount", manifest.IntegrityFailureCount, true);
        writer.Write("  \"balanceBaselineRecordIds\": [");
        for (int index = 0; index < manifest.BalanceBaselineRecordIds.Count; index++)
        {
            if (index != 0)
                writer.Write(',');
            V27BalanceJsonSerializer.WriteJsonString(
                writer,
                manifest.BalanceBaselineRecordIds[index]);
        }
        writer.Write("]\n");
        writer.Write("}\n");
        writer.Flush();
    }

    private static void WriteEvidenceHash(
        StreamWriter writer,
        string propertyName,
        string absolutePath) => WriteJsonProperty(
        writer,
        propertyName,
        File.Exists(absolutePath) ? HashFile(absolutePath) : HashText(string.Empty),
        true);

    private static void WriteJsonProperty(
        StreamWriter writer,
        string name,
        string value,
        bool comma)
    {
        writer.Write("  \""); writer.Write(name); writer.Write("\": ");
        V27BalanceJsonSerializer.WriteJsonString(writer, value);
        if (comma) writer.Write(',');
        writer.Write('\n');
    }

    private static void WriteJsonNumber(
        StreamWriter writer,
        string name,
        int value,
        bool comma)
    {
        writer.Write("  \""); writer.Write(name); writer.Write("\": ");
        writer.Write(value);
        if (comma) writer.Write(',');
        writer.Write('\n');
    }

    private static StreamWriter NewLfWriter(Stream stream) => new StreamWriter(
        stream,
        new UTF8Encoding(false, true),
        16384,
        leaveOpen: true) { NewLine = "\n" };

    private static string AuditDispositionToken(BalanceAnomalyDisposition disposition) =>
        disposition switch
        {
            BalanceAnomalyDisposition.None => "NONE",
            BalanceAnomalyDisposition.RootCritical => "ROOTCRITICAL",
            BalanceAnomalyDisposition.LocalCritical => "LOCALCRITICAL",
            BalanceAnomalyDisposition.CollapsedInheritedOnly => "COLLAPSEDINHERITEDONLY",
            BalanceAnomalyDisposition.CollapsedRoundingOnly => "COLLAPSEDROUNDINGONLY",
            BalanceAnomalyDisposition.CollapsedMultiRoot => "COLLAPSEDMULTIROOT",
            BalanceAnomalyDisposition.Approved => "APPROVED",
            _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, null)
        };

    private static string DispositionToken(BalanceAnomalyDisposition disposition) =>
        disposition switch
        {
            BalanceAnomalyDisposition.None => "none",
            BalanceAnomalyDisposition.RootCritical => "root-critical",
            BalanceAnomalyDisposition.LocalCritical => "local-critical",
            BalanceAnomalyDisposition.CollapsedInheritedOnly => "collapsed-inherited-only",
            BalanceAnomalyDisposition.CollapsedRoundingOnly => "collapsed-rounding-only",
            BalanceAnomalyDisposition.CollapsedMultiRoot => "collapsed-multi-root",
            BalanceAnomalyDisposition.Approved => "approved",
            _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, null)
        };

    private static void WriteMarkdownCell(StreamWriter writer, string value)
    {
        ReadOnlySpan<char> text = (value ?? string.Empty).AsSpan();
        int segmentStart = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] != '|' && text[index] != '\\')
                continue;
            writer.Write(text.Slice(segmentStart, index - segmentStart));
            writer.Write('\\');
            writer.Write(text[index]);
            segmentStart = index + 1;
        }
        writer.Write(text.Slice(segmentStart));
    }

    private static bool TryGetSerializedToken(
        SerializedProperty property,
        out string value,
        out string unit)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                value = property.longValue.ToString(CultureInfo.InvariantCulture);
                unit = "integer";
                return true;
            case SerializedPropertyType.Boolean:
                value = property.boolValue ? "true" : "false";
                unit = "boolean";
                return true;
            case SerializedPropertyType.Float:
                double number = property.doubleValue;
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new InvalidOperationException(
                        $"Non-finite serialized authority: {property.propertyPath}");
                value = number.ToString("R", CultureInfo.InvariantCulture);
                unit = "number";
                return true;
            case SerializedPropertyType.Enum:
                value = property.enumValueIndex.ToString(CultureInfo.InvariantCulture);
                unit = "enum";
                return true;
            default:
                value = null;
                unit = null;
                return false;
        }
    }

    private static bool ShouldEnterSerializedChildren(SerializedProperty property)
    {
        if (property == null || !property.hasVisibleChildren)
            return false;
        return property.propertyType == SerializedPropertyType.Generic
            || property.propertyType == SerializedPropertyType.ManagedReference;
    }

    private static string FindUniqueSerializedPropertyPath(
        UnityEngine.Object authority,
        string propertyName)
    {
        SerializedObject serialized = new SerializedObject(authority);
        SerializedProperty iterator = serialized.GetIterator();
        List<string> paths = new List<string>();
        bool enterChildren = true;
        while (iterator.Next(enterChildren))
        {
            enterChildren = ShouldEnterSerializedChildren(iterator);
            if (string.Equals(iterator.name, propertyName, StringComparison.Ordinal))
                paths.Add(iterator.propertyPath);
        }
        string[] distinct = paths.Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one SerializedProperty named {propertyName} on "
                + $"{AssetDatabase.GetAssetPath(authority)}, found {distinct.Length}.");
        }
        return distinct[0];
    }

    private static string RawStableId(UnityEngine.Object authority, string propertyName)
    {
        SerializedObject serialized = new SerializedObject(authority);
        SerializedProperty property = serialized.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"Stable ID property '{propertyName}' is missing on {authority.name}.");
        return BalanceCanonicalText.StableId(
            property.stringValue,
            AssetDatabase.GetAssetPath(authority) + ":" + propertyName);
    }

    private static string ResolveBuildingStableId(BuildingSO building)
    {
        SerializedObject serialized = new SerializedObject(building);
        SerializedProperty property = serialized.FindProperty("contentDefinitionId")
            ?? throw new InvalidOperationException(
                $"Building identity property is missing: {AssetDatabase.GetAssetPath(building)}");
        if (!string.IsNullOrEmpty(property.stringValue))
        {
            return BalanceCanonicalText.StableId(
                property.stringValue,
                AssetDatabase.GetAssetPath(building) + ":contentDefinitionId");
        }

        if (building.id < 0)
            throw new InvalidOperationException(
                $"Building has neither contentDefinitionId nor non-negative numeric authority: "
                + AssetDatabase.GetAssetPath(building));
        return BalanceCanonicalText.StableId(
            "building:" + building.id.ToString(CultureInfo.InvariantCulture),
            AssetDatabase.GetAssetPath(building) + ":id");
    }

    private static Dictionary<string, int> BuildDownstreamCounts(
        IEnumerable<ProductionRecipeSO> recipes,
        IEnumerable<CropDefinitionSO> crops)
    {
        Dictionary<string, HashSet<string>> consumers =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (ProductionRecipeSO recipe in recipes)
        {
            foreach (ItemAmountDefinition input in recipe.Inputs)
            {
                if (!consumers.TryGetValue(input.ItemId, out HashSet<string> set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    consumers.Add(input.ItemId, set);
                }
                set.Add(recipe.RecipeId);
            }
        }
        foreach (CropDefinitionSO crop in crops)
        {
            string[] inputs = crop.DailyWater > 0f
                ? new[] { crop.SeedItemId, "resource:clean-water" }
                : new[] { crop.SeedItemId };
            foreach (string itemId in inputs)
            {
                if (!consumers.TryGetValue(itemId, out HashSet<string> set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    consumers.Add(itemId, set);
                }
                set.Add(crop.CropId);
            }
        }
        return consumers.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Count,
            StringComparer.Ordinal);
    }

    private readonly struct CropCostCandidate
    {
        public CropCostCandidate(
            EwuAmount inputDebit,
            EwuAmount directWorkDebit,
            EwuAmount logisticsDebit,
            EwuAmount infrastructureDebit,
            EwuAmount expectedLossDebit,
            EwuAmount perUnitAcquisition)
        {
            InputDebit = inputDebit;
            DirectWorkDebit = directWorkDebit;
            LogisticsDebit = logisticsDebit;
            InfrastructureDebit = infrastructureDebit;
            ExpectedLossDebit = expectedLossDebit;
            PerUnitAcquisition = perUnitAcquisition;
        }

        public EwuAmount InputDebit { get; }
        public EwuAmount DirectWorkDebit { get; }
        public EwuAmount LogisticsDebit { get; }
        public EwuAmount InfrastructureDebit { get; }
        public EwuAmount ExpectedLossDebit { get; }
        public EwuAmount PerUnitAcquisition { get; }
    }

    private static string ResolveDomain(string path, string typeName)
    {
        string probe = path + "/" + typeName;
        if (probe.IndexOf("Research", StringComparison.OrdinalIgnoreCase) >= 0) return "research";
        if (probe.IndexOf("Medical", StringComparison.OrdinalIgnoreCase) >= 0
            || probe.IndexOf("Surgery", StringComparison.OrdinalIgnoreCase) >= 0) return "medical";
        if (probe.IndexOf("Combat", StringComparison.OrdinalIgnoreCase) >= 0
            || probe.IndexOf("Equipment", StringComparison.OrdinalIgnoreCase) >= 0) return "combat";
        if (probe.IndexOf("Crop", StringComparison.OrdinalIgnoreCase) >= 0
            || probe.IndexOf("Husbandry", StringComparison.OrdinalIgnoreCase) >= 0) return "agriculture";
        if (probe.IndexOf("Recipe", StringComparison.OrdinalIgnoreCase) >= 0
            || probe.IndexOf("Production", StringComparison.OrdinalIgnoreCase) >= 0) return "production";
        if (probe.IndexOf("Building", StringComparison.OrdinalIgnoreCase) >= 0) return "facilities";
        if (probe.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0
            || probe.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0) return "items";
        if (probe.IndexOf("Contract", StringComparison.OrdinalIgnoreCase) >= 0
            || probe.IndexOf("Economy", StringComparison.OrdinalIgnoreCase) >= 0) return "economy";
        if (probe.IndexOf("Invasion", StringComparison.OrdinalIgnoreCase) >= 0
            || probe.IndexOf("Defense", StringComparison.OrdinalIgnoreCase) >= 0) return "defense";
        if (probe.IndexOf("Offense", StringComparison.OrdinalIgnoreCase) >= 0
            || probe.IndexOf("Expedition", StringComparison.OrdinalIgnoreCase) >= 0) return "offense";
        return "content";
    }

    private static string FormatBom(IEnumerable<ItemAmountDefinition> values) =>
        string.Join("|", (values ?? Array.Empty<ItemAmountDefinition>())
            .Where(value => value != null)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .Select(value => value.ItemId + "=" + value.Amount.ToString(CultureInfo.InvariantCulture)));

    private static string GetSourceDigest(
        string projectRelativePath,
        IDictionary<string, string> cache)
    {
        string path = BalanceCanonicalText.ProjectRelativePath(projectRelativePath);
        if (!cache.TryGetValue(path, out string digest))
        {
            digest = HashCanonicalSourceFile(ProjectAbsolutePath(path));
            cache.Add(path, digest);
        }
        return digest;
    }

    private static void CapturePipelineSourceDigests(IDictionary<string, string> cache)
    {
        string[] paths =
        {
            "Assets/Scripts/Services/Economy/V27BalanceLedgerCore.cs",
            "Assets/Scripts/Services/Economy/V27BalanceAttribution.cs",
            "Assets/Scripts/Services/Economy/V27EmbeddedWorkValueCalculator.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceAudit.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceSerialization.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceAssetApplication.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceAssetRollbackDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceEconomySimulationDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceMarketDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceMarketDecisionEpoch.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceMarketReviewPromotion.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceMarketApplicationReceiptDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceMarketApplicationReceiptV2.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceConstructionReviewPromotion.cs",
            "Assets/Resources/SO/Factions/FactionAllianceBenefitBudget.asset",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceLaborFacilityDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/Editor/V27ConstructionRedistributionPolicy.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceWholeGameCoverageDebugScenarios.cs",
            "Assets/Scripts/Services/Character/Work/Editor/V27LaborAuthorityMatrixDebugScenarios.cs",
            "Assets/Scripts/Services/Character/Work/Editor/DailyRoutineWuPlayModeVerifier.cs",
            "Assets/Scripts/Services/Character/Work/SettlementLaborBalanceRules.cs",
            "Assets/Scripts/Models/Work/SettlementLaborAuthority.cs",
            "Assets/Scripts/Services/Economy/Editor/BranchedProductionNetworkDebugScenarios.cs",
            "Assets/Scripts/Services/Offense/Editor/CombatOutcomeBalanceCalibrationScenario.cs",
            "Assets/Scripts/Services/Offense/Editor/CombatBalanceCheckpointAuthority.cs",
            "Assets/Scripts/Services/Offense/Editor/SettlementEquipmentReadinessThroughputDebugScenarios.cs",
            "Assets/Scripts/Services/Offense/Editor/V20CombatContentAssetBuilder.cs",
            "Assets/Scripts/Services/Offense/EnemyEncounterFactory.cs",
            "Assets/Scripts/Services/Offense/EnemyTacticalDecisionService.cs",
            "Assets/Scripts/Services/Offense/OffenseBattleModel.cs",
            "Assets/Scripts/Models/Offense/Core/OffenseEncounterBalanceRules.cs",
            "Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs",
            "Assets/Scripts/Models/Offense/Core/OffenseBattleContracts.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceVerticalSlicePlayModeVerifier.cs",
            "Assets/Scripts/Services/Economy/V27BalanceWorkCalculator.cs",
            "Assets/Scripts/Models/Buildings/Core/StockCategoryCatalog.cs",
            "Assets/Scripts/Services/Buildings/SO/StockInfo.cs",
            "Assets/Scripts/Services/Survival/SurvivalFoodRuntime.cs",
            "Assets/Scripts/Services/Items/GameContentCatalog.cs",
            ".gitattributes",
            ".github/workflows/v27-ledger-integrity.yml",
            "tools/V27Balance/verify_committed_artifacts.py",
            "tools/DungeonStory.BalanceAnalyzers/DungeonStoryBalanceAnalyzer.cs",
            "tools/DungeonStory.BalanceAnalyzers/verify_analyzer.py",
            "tools/DungeonStory.BalanceAnalyzers/build-analyzer.ps1",
            "tools/DungeonStory.BalanceAnalyzers/test-analyzer.ps1",
            "tools/DungeonStory.BalanceAnalyzers/Tests/Positive.cs",
            "tools/DungeonStory.BalanceAnalyzers/Tests/Negative.cs",
            "docs/game-design/whole-game-balance-baseline.md"
        };
        foreach (string path in paths)
            GetSourceDigest(path, cache);
    }

    private static string BuildApprovalKey(
        string stableId,
        string metric,
        string after,
        string dependencyFingerprint,
        string sourceDigest,
        string reasonCode,
        string baselineRecordId) => HashText(
            stableId + "\u001f" + metric + "\u001f" + after + "\u001f"
            + dependencyFingerprint + "\u001f" + sourceDigest + "\u001f"
            + reasonCode + "\u001f" + baselineRecordId);

    private static string HashCanonicalPairs(IReadOnlyDictionary<string, string> pairs)
    {
        using SHA256 sha = SHA256.Create();
        using MemoryStream stream = new MemoryStream();
        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
        {
            foreach (KeyValuePair<string, string> pair in pairs
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                writer.Write(pair.Key); writer.Write('='); writer.Write(pair.Value); writer.Write('\n');
            }
            writer.Flush();
        }
        stream.Position = 0L;
        return Hex(sha.ComputeHash(stream));
    }

    internal static string BuildMarketAuthoritySemanticHash(
        string stableId,
        string authorityMetric,
        string exactAfterToken) => HashText(
        stableId + "|" + authorityMetric + "|" + exactAfterToken);

    private static string HashText(string value)
    {
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(new UTF8Encoding(false, true).GetBytes(value ?? string.Empty)));
    }

    private static string HashFile(string absolutePath)
    {
        using FileStream stream = File.OpenRead(absolutePath);
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(stream));
    }

    private static string HashCanonicalSourceFile(string absolutePath)
    {
        byte[] bytes = File.ReadAllBytes(absolutePath);
        using MemoryStream normalized = new MemoryStream(bytes.Length);
        for (int index = 0; index < bytes.Length; index++)
        {
            byte value = bytes[index];
            if (value != (byte)'\r')
            {
                normalized.WriteByte(value);
                continue;
            }

            if (index + 1 < bytes.Length && bytes[index + 1] == (byte)'\n')
                index++;
            normalized.WriteByte((byte)'\n');
        }

        normalized.Position = 0L;
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(normalized));
    }

    private static string HashDirectory(string absolutePath)
    {
        Dictionary<string, string> hashes = Directory
            .EnumerateFiles(absolutePath, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => path.Substring(absolutePath.Length).TrimStart('\\', '/').Replace('\\', '/'),
                HashFile,
                StringComparer.Ordinal);
        return HashCanonicalPairs(hashes);
    }

    private static string Hex(byte[] bytes)
    {
        const string Digits = "0123456789abcdef";
        char[] characters = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            characters[index * 2] = Digits[bytes[index] >> 4];
            characters[index * 2 + 1] = Digits[bytes[index] & 0xf];
        }
        return new string(characters);
    }

    private static string Token(decimal value) =>
        BalanceCanonicalText.InvariantDecimal(value);

    private static string ResolveBaselineRecordId(string stableId)
    {
        return stableId switch
        {
            "source:logging" or
            "source:quarry" or
            "recipe:resource:clean-water" or
            "recipe:sawmill-lumber" or
            "recipe:treated-lumber" or
            "recipe:grain-porridge" or
            "crop:twilight-grain" or
            "building:1002" or
            "resource:clean-water" or
            "resource:log" or
            "resource:dark-resin" or
            "material:lumber" or
            "material:treated-lumber" or
            "material:iron-ingot" or
            "material:stone-block" or
            "resource:twilight-grain" or
            "food:grain-porridge" => VerticalSliceBaselineRecordId,
            _ => BaselineRecordId
        };
    }

    private static string ResolveLaborBaselineRecordId(string stableId) =>
        string.Equals(ResolveBaselineRecordId(stableId), VerticalSliceBaselineRecordId,
            StringComparison.Ordinal)
            ? VerticalSliceBaselineRecordId
            : LaborFacilityBaselineRecordId;

    private static decimal ResolveCropAuthoredBefore(
        string cropId,
        string propertyPath,
        decimal current,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        string metric = propertyPath switch
        {
            "sowWork" => "authored-sow-wu",
            "harvestWork" => "authored-harvest-wu",
            _ => throw new InvalidOperationException(
                $"Unknown crop work property: {cropId}:{propertyPath}.")
        };
        decimal historical = ResolveHistoricalAuthoredBefore(
            cropId,
            metric,
            current,
            historicalBeforeValues);
        if (historical != current || historicalBeforeValues != null)
            return historical;
        if (!string.Equals(cropId, "crop:twilight-grain", StringComparison.Ordinal))
            return current;
        return propertyPath switch
        {
            "sowWork" => 3m,
            "harvestWork" => 6m,
            _ => throw new InvalidOperationException(
                $"Unknown frozen crop work authority: {cropId}:{propertyPath}.")
        };
    }

    private static decimal ResolveHistoricalAuthoredBefore(
        string stableId,
        string metric,
        decimal current,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        if (historicalBeforeValues != null
            && historicalBeforeValues.TryGetValue(
                V27BalanceAssetApplication.BuildHistoricalBeforeKey(stableId, metric),
                out string token))
        {
            return decimal.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        return current;
    }

    private static string GetApprovalSourceDigest(
        string projectRelativePath,
        params string[] yamlFieldNames)
    {
        return GetApprovalSourceDigestCore(
            projectRelativePath,
            requireExactRequestedFields: true,
            yamlFieldNames);
    }

    private static string GetEncounterApprovalSourceDigest(string projectRelativePath)
    {
        string[] mutableFields =
        {
            "additionalEnemyCount",
            "enemyAccuracyMultiplier",
            "enemyDamageMultiplier",
            "enemyHealthMultiplier",
            "objectiveControlResistanceMultiplier",
            "objectiveHealthMultiplier",
            "objectiveRoundLimit"
        };
        HashSet<string> prefixes = mutableFields
            .Select(value => value + ":")
            .ToHashSet(StringComparer.Ordinal);
        string absolutePath = ProjectAbsolutePath(projectRelativePath);
        string[] stableLines = File.ReadAllText(absolutePath, Encoding.UTF8)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Where(line => !prefixes.Any(prefix =>
                line.TrimStart().StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();
        return HashText(string.Join("\n", stableLines));
    }

    private static string GetApprovalSourceDigestCore(
        string projectRelativePath,
        bool requireExactRequestedFields,
        params string[] yamlFieldNames)
    {
        if (yamlFieldNames == null || yamlFieldNames.Length == 0)
            throw new ArgumentException("At least one YAML field is required.", nameof(yamlFieldNames));
        string absolutePath = ProjectAbsolutePath(projectRelativePath);
        string[] lines = File.ReadAllText(absolutePath, Encoding.UTF8)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        HashSet<string> required = yamlFieldNames.ToHashSet(StringComparer.Ordinal);
        MaskApprovalYamlScalars(
            lines,
            required,
            requireExactRequestedFields,
            projectRelativePath);
        return HashText(string.Join("\n", lines));
    }

    private static string GetConstructionMaterialApprovalSourceDigest(
        string projectRelativePath,
        string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException(
                "Construction material approval requires an item ID.",
                nameof(itemId));
        }

        string absolutePath = ProjectAbsolutePath(projectRelativePath);
        string[] lines = File.ReadAllText(absolutePath, Encoding.UTF8)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        int matched = 0;
        for (int containerIndex = 0;
             containerIndex < lines.Length;
             containerIndex++)
        {
            string containerTrimmed = lines[containerIndex].TrimStart();
            if (!string.Equals(
                    containerTrimmed,
                    "constructionMaterials:",
                    StringComparison.Ordinal))
            {
                continue;
            }

            int containerIndent =
                lines[containerIndex].Length - containerTrimmed.Length;
            for (int itemIndex = containerIndex + 1;
                 itemIndex < lines.Length;
                 itemIndex++)
            {
                string itemTrimmed = lines[itemIndex].TrimStart();
                int itemIndent = lines[itemIndex].Length - itemTrimmed.Length;
                if (itemTrimmed.Length > 0
                    && (itemIndent < containerIndent
                        || itemIndent == containerIndent
                        && !itemTrimmed.StartsWith("- ", StringComparison.Ordinal)))
                {
                    break;
                }

                const string itemPrefix = "- itemId: ";
                if (!itemTrimmed.StartsWith(itemPrefix, StringComparison.Ordinal)
                    || !string.Equals(
                        itemTrimmed.Substring(itemPrefix.Length),
                        itemId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                bool amountFound = false;
                for (int amountIndex = itemIndex + 1;
                     amountIndex < lines.Length;
                     amountIndex++)
                {
                    string amountTrimmed = lines[amountIndex].TrimStart();
                    int amountIndent =
                        lines[amountIndex].Length - amountTrimmed.Length;
                    if (amountTrimmed.Length > 0
                        && amountIndent <= itemIndent)
                    {
                        break;
                    }
                    if (!amountTrimmed.StartsWith("amount:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    lines[amountIndex] = new string(' ', amountIndent)
                        + "amount: <v27-approved-target>";
                    amountFound = true;
                    matched++;
                    break;
                }

                if (!amountFound)
                {
                    throw new InvalidOperationException(
                        $"V27 approval digest found construction material '{itemId}' "
                        + $"without an amount in {projectRelativePath}.");
                }
            }
        }

        if (matched != 1)
        {
            throw new InvalidOperationException(
                $"V27 approval digest requires exactly one construction material "
                + $"'{itemId}' in {projectRelativePath}; found {matched}.");
        }

        MaskApprovalYamlScalars(
            lines,
            new HashSet<string>(StringComparer.Ordinal),
            requireExactRequestedFields: false,
            projectRelativePath);
        return HashText(string.Join("\n", lines));
    }

    private static void MaskApprovalYamlScalars(
        string[] lines,
        HashSet<string> required,
        bool requireExactRequestedFields,
        string projectRelativePath)
    {
        MaskConstructionMaterialAmounts(lines);
        string[] mutableBalanceFields = required
            .Concat(new[]
            {
                "constructionWorkRequired",
                "additionalEnemyCount",
                "enemyAccuracyMultiplier",
                "enemyDamageMultiplier",
                "enemyHealthMultiplier",
                "harvestWork",
                "objectiveControlResistanceMultiplier",
                "objectiveHealthMultiplier",
                "objectiveRoundLimit",
                "requiredWork",
                "saleRate",
                "sowWork",
                "unitPrice"
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        foreach (string yamlFieldName in mutableBalanceFields)
        {
            string prefix = yamlFieldName + ":";
            int matched = 0;
            for (int index = 0; index < lines.Length; index++)
            {
                string trimmed = lines[index].TrimStart();
                if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                int indentation = lines[index].Length - trimmed.Length;
                lines[index] = new string(' ', indentation)
                    + prefix
                    + " <v27-approved-target>";
                matched++;
            }
            if (requireExactRequestedFields
                && required.Contains(yamlFieldName)
                && matched != 1)
            {
                throw new InvalidOperationException(
                    $"V27 approval digest requires exactly one YAML scalar '{yamlFieldName}' "
                    + $"in {projectRelativePath}; found {matched}.");
            }
        }
    }

    private static int DependencyRootMetricPriority(string metric)
    {
        if (string.Equals(metric, "authored-unit-price-gold", StringComparison.Ordinal))
            return 0;
        if (string.Equals(metric, "acquisition-cost", StringComparison.Ordinal)
            || string.Equals(metric, "cultivated-acquisition-cost", StringComparison.Ordinal))
            return 1;
        if (string.Equals(metric, "direct-wu", StringComparison.Ordinal))
            return 2;
        if (string.Equals(metric, "authored-required-wu", StringComparison.Ordinal)
            || string.Equals(metric, "authored-sow-wu", StringComparison.Ordinal)
            || string.Equals(metric, "authored-harvest-wu", StringComparison.Ordinal)
            || string.Equals(
                metric,
                "construction-authored-wu:redistributed",
                StringComparison.Ordinal))
        {
            return 3;
        }
        if (!string.IsNullOrEmpty(metric)
            && metric.StartsWith(
                "construction-material-amount:",
                StringComparison.Ordinal))
        {
            return 4;
        }
        return int.MaxValue;
    }

    private static int MaskConstructionMaterialAmounts(string[] lines)
    {
        int masked = 0;
        for (int containerIndex = 0;
             containerIndex < lines.Length;
             containerIndex++)
        {
            string containerTrimmed = lines[containerIndex].TrimStart();
            if (!string.Equals(
                    containerTrimmed,
                    "constructionMaterials:",
                    StringComparison.Ordinal))
            {
                continue;
            }

            int containerIndent =
                lines[containerIndex].Length - containerTrimmed.Length;
            for (int itemIndex = containerIndex + 1;
                 itemIndex < lines.Length;
                 itemIndex++)
            {
                string itemTrimmed = lines[itemIndex].TrimStart();
                int itemIndent = lines[itemIndex].Length - itemTrimmed.Length;
                if (itemTrimmed.Length > 0
                    && (itemIndent < containerIndent
                        || itemIndent == containerIndent
                        && !itemTrimmed.StartsWith("- ", StringComparison.Ordinal)))
                {
                    break;
                }
                if (!itemTrimmed.StartsWith("- itemId: ", StringComparison.Ordinal))
                    continue;

                for (int amountIndex = itemIndex + 1;
                     amountIndex < lines.Length;
                     amountIndex++)
                {
                    string amountTrimmed = lines[amountIndex].TrimStart();
                    int amountIndent =
                        lines[amountIndex].Length - amountTrimmed.Length;
                    if (amountTrimmed.Length > 0 && amountIndent <= itemIndent)
                        break;
                    if (!amountTrimmed.StartsWith("amount:", StringComparison.Ordinal))
                        continue;

                    lines[amountIndex] = new string(' ', amountIndent)
                        + "amount: <v27-approved-target>";
                    masked++;
                    break;
                }
            }
        }
        return masked;
    }

    private static decimal PercentDelta(decimal before, decimal after)
    {
        if (before == 0m)
            return after == 0m ? 0m : 1000000m;
        return checked((after - before) / Math.Abs(before) * 100m);
    }

    private static decimal PercentDelta(long before, long after) =>
        PercentDelta((decimal)before, after);

    private static BalanceAnomalySeverity Max(
        BalanceAnomalySeverity left,
        BalanceAnomalySeverity right) => left >= right ? left : right;

    private static string ProjectAbsolutePath(string projectRelativePath)
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        return Path.Combine(
            root,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class V27EditorContentSource : IGameContentDefinitionSource
    {
        private readonly GameDomainContentCatalogSO domain;
        private readonly ItemDefinitionCatalogSO items;

        private V27EditorContentSource(
            GameDomainContentCatalogSO domain,
            ItemDefinitionCatalogSO items)
        {
            this.domain = domain;
            this.items = items;
            AllDefinitions = domain.Definitions
                .Concat(items.Definitions.Cast<ScriptableObject>())
                .Where(value => value != null)
                .Distinct()
                .ToArray();
        }

        public IReadOnlyList<ScriptableObject> AllDefinitions { get; }
        public GameDomainContentCatalogSO DomainCatalog => domain;

        public static V27EditorContentSource Load()
        {
            GameContentCatalogSO root = Resources.Load<GameContentCatalogSO>(
                GameContentCatalogSO.ResourcePath)
                ?? throw new InvalidOperationException("Root content catalog is missing.");
            GameDomainContentCatalogSO domain = root.DomainCatalogs
                .OfType<GameDomainContentCatalogSO>()
                .Single();
            ItemDefinitionCatalogSO items = root.GetItemDefinitions<ItemDefinitionCatalogSO>()
                ?? throw new InvalidOperationException("Item definition catalog is missing.");
            return new V27EditorContentSource(domain, items);
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject =>
            AllDefinitions.OfType<T>().Distinct().ToArray();

        public T RequireSingle<T>() where T : ScriptableObject => GetAll<T>().Single();
    }
}

public sealed class V27BalanceAuditOutput
{
    public V27BalanceAuditOutput(
        BalanceAuthoritySnapshot authoritySnapshot,
        BalanceArtifactManifest artifactManifest,
        string assetPatchDigest,
        IReadOnlyList<BalanceAnomalyNode> anomalies,
        IReadOnlyList<string> integrityFailures,
        V27BalanceAuditWriteResult writeResult)
    {
        AuthoritySnapshot = authoritySnapshot
            ?? throw new ArgumentNullException(nameof(authoritySnapshot));
        ArtifactManifest = artifactManifest
            ?? throw new ArgumentNullException(nameof(artifactManifest));
        AssetPatchDigest = assetPatchDigest ?? string.Empty;
        Anomalies = anomalies ?? throw new ArgumentNullException(nameof(anomalies));
        IntegrityFailures = integrityFailures
            ?? throw new ArgumentNullException(nameof(integrityFailures));
        WriteResult = writeResult
            ?? throw new ArgumentNullException(nameof(writeResult));
    }

    public BalanceAuthoritySnapshot AuthoritySnapshot { get; }
    public BalanceArtifactManifest ArtifactManifest { get; }
    public string AssetPatchDigest { get; }
    public IReadOnlyList<BalanceAnomalyNode> Anomalies { get; }
    public FrozenBalanceLedger Ledger => AuthoritySnapshot.Ledger;
    public int CriticalCount => ArtifactManifest.CriticalCount;
    public int SccCount => ArtifactManifest.SccCount;
    public IReadOnlyList<string> IntegrityFailures { get; }
    public V27BalanceAuditWriteResult WriteResult { get; }
}

[BalanceImmutableRecord]
public sealed class V27BalanceAuditWriteResult
{
    private static readonly string[] ExpectedPaths =
    {
        V27BalanceCsvSerializer.ArtifactPath,
        V27BalanceAudit.MarkdownPath,
        V27BalanceJsonSerializer.AnomalyArtifactPath,
        V27BalanceAudit.AuditPath,
        V27BalanceAudit.SourceInventoryPath,
        V27BalanceAudit.ManifestPath
    };

    private V27BalanceAuditWriteResult(
        V27BalanceAuditWriteObservation[] observations)
    {
        Observations = Array.AsReadOnly(observations);
    }

    public IReadOnlyList<V27BalanceAuditWriteObservation> Observations { get; }
    public int InvocationCount => Observations.Count;
    public bool CsvChanged => Observations[0].Changed;
    public bool MarkdownChanged => Observations[1].Changed;
    public bool AnomalyChanged => Observations[2].Changed;
    public bool AuditChanged => Observations[3].Changed;
    public bool SourceInventoryChanged => Observations[4].Changed;
    public bool ManifestChanged => Observations[5].Changed;
    public int ChangedCount => Observations.Count(value => value.Changed);

    [BalanceCaptureFactory]
    public static V27BalanceAuditWriteResult Capture(
        IReadOnlyList<V27BalanceAuditWriteObservation> observations)
    {
        if (observations == null)
            throw new ArgumentNullException(nameof(observations));
        if (observations.Count != ExpectedPaths.Length)
            throw new InvalidOperationException(
                "Audit writer invocation count must be exactly six.");
        V27BalanceAuditWriteObservation[] copy = observations.ToArray();
        for (int index = 0; index < copy.Length; index++)
        {
            if (!string.Equals(copy[index].ProjectRelativePath,
                    ExpectedPaths[index], StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Audit writer invocation path/order drift at index " + index + ".");
            }
        }
        return new V27BalanceAuditWriteResult(copy);
    }
}

public readonly struct V27BalanceAuditWriteObservation
{
    private V27BalanceAuditWriteObservation(
        string projectRelativePath,
        bool changed)
    {
        ProjectRelativePath = projectRelativePath;
        Changed = changed;
    }

    public string ProjectRelativePath { get; }
    public bool Changed { get; }

    [BalanceCaptureFactory]
    public static V27BalanceAuditWriteObservation Capture(
        string projectRelativePath,
        bool changed) => new V27BalanceAuditWriteObservation(
            BalanceCanonicalText.ProjectRelativePath(projectRelativePath),
            changed);
}
#endif
