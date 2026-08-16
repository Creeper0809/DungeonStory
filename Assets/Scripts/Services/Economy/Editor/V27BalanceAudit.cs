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
    public const string MarkdownPath = "docs/generated/V27_Balance_Before_After.md";
    public const string AuditPath = "Artifacts/QA/v27-balance-recalibration-audit.txt";
    public const string ManifestPath = "Artifacts/QA/v27-balance-artifact-manifest.json";
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
        "balance:v27:global-labor-facility-period-preserving";
    private const string GeneratorVersion = "v27.5.0";
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
        if (recipes.Length != 354)
            integrityFailures.Add($"Expected 354 recipes, found {recipes.Length}.");
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
        Dictionary<string, string> sourceDigests = new Dictionary<string, string>(
            StringComparer.Ordinal);
        CapturePipelineSourceDigests(sourceDigests);
        BalanceCaptureFactory capture = new BalanceCaptureFactory();
        List<BalanceAnomalyNode> anomalies = new List<BalanceAnomalyNode>();
        CaptureLaborTargets(capture, anomalies, sourceDigests);
        CaptureSerializedAuthority(source, capture, sourceDigests);
        CaptureItemValues(
            items,
            before,
            after,
            materialProfiles,
            downstream,
            capture,
            anomalies,
            sourceDigests,
            integrityFailures,
            historicalBeforeValues);
        CaptureItemMarketConsumers(
            source,
            items,
            before,
            after,
            capture,
            sourceDigests,
            integrityFailures,
            historicalBeforeValues);
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
        CaptureBuildingCandidates(
            source.GetAll<BuildingSO>(),
            before,
            after,
            work,
            capture,
            anomalies,
            sourceDigests,
            historicalBeforeValues);
        CaptureDismantleCycles(
            source.GetAll<BuildingSO>(),
            before,
            after,
            work,
            salvage,
            capture,
            anomalies,
            sourceDigests);

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
                work,
                salvage))
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
        PromoteDependencyRoots(ledger, anomalies);
        string[] approvedKeys = allowApprovalRefresh
            ? V27BalanceAssetApplication.CaptureMatchingApprovalKeysForRefresh(ledger)
            : V27BalanceAssetApplication.CaptureValidApprovalKeys(ledger);
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

        V27BalanceArtifactWriter.WriteCsvIfDifferent(
            V27BalanceCsvSerializer.ArtifactPath,
            ledger);
        V27BalanceArtifactWriter.WriteIfDifferent(MarkdownPath, stream =>
            WriteMarkdown(stream, ledger, orderedAnomalies, scc, integrityFailures));
        V27BalanceArtifactWriter.WriteIfDifferent(
            V27BalanceJsonSerializer.AnomalyArtifactPath,
            stream => V27BalanceJsonSerializer.WriteAnomalyGraph(stream, orderedAnomalies));
        V27BalanceArtifactWriter.WriteIfDifferent(AuditPath, stream =>
            WriteAudit(stream, ledger, orderedAnomalies, scc, integrityFailures));

        string aggregateSourceDigest = HashCanonicalPairs(sourceDigests);
        string csvHash = V27BalanceArtifactWriter.ComputeSha256(
            V27BalanceCsvSerializer.ArtifactPath);
        string markdownHash = V27BalanceArtifactWriter.ComputeSha256(MarkdownPath);
        string auditHash = V27BalanceArtifactWriter.ComputeSha256(AuditPath);
        string anomalyHash = V27BalanceArtifactWriter.ComputeSha256(
            V27BalanceJsonSerializer.AnomalyArtifactPath);
        string approvalHash = V27BalanceArtifactWriter.ComputeSha256(ApprovalPath);
        V27BalanceArtifactWriter.WriteIfDifferent(ManifestPath, stream =>
            WriteManifest(
                stream,
                ledger,
                aggregateSourceDigest,
                csvHash,
                markdownHash,
                auditHash,
                anomalyHash,
                approvalHash,
                assetPatchDigest,
                approvedKeys.Length,
                orderedAnomalies,
                scc,
                integrityFailures));
        AssetDatabase.Refresh();
        return new V27BalanceAuditOutput(
            ledger,
            orderedAnomalies.Count(value => value.EmitsCiAnnotation),
            scc.Components.Count,
            Array.AsReadOnly(integrityFailures.ToArray()));
    }

    private static void CaptureLaborTargets(
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests)
    {
        const string sourcePath = "docs/game-design/whole-game-balance-baseline.md";
        string sourceDigest = GetSourceDigest(sourcePath, sourceDigests);
        decimal[] actual = { 50m, 54.5m, 62.5m, 74.5m, 85m, 100m };
        decimal[] effective = { 45m, 49.05m, 56.25m, 67.05m, 76.5m, 90m };
        for (int stage = 0; stage < actual.Length; stage++)
        {
            CaptureLaborMetric(
                capture, anomalies, sourcePath, sourceDigest, stage,
                "actual-wu-per-adult-day", 20m * ResolveLegacyStageMultiplier(stage), actual[stage]);
            CaptureLaborMetric(
                capture, anomalies, sourcePath, sourceDigest, stage,
                "effective-wu-per-adult-day", 20m * ResolveLegacyStageMultiplier(stage), effective[stage]);
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

    private static decimal ResolveLegacyStageMultiplier(int stage) => stage switch
    {
        0 => 1m,
        1 => 1.092m,
        2 => 1.254m,
        3 => 1.4942m,
        4 => 1.6974m,
        5 => 2m,
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

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
        IReadOnlyDictionary<string, int> downstream,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests,
        ICollection<string> integrityFailures,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
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
            CaptureItemMetric(
                definition,
                "acquisition-cost",
                beforeMilli,
                value.AcquisitionCost.MilliEwu,
                value.SelectedSourceId,
                downstream.TryGetValue(definition.ItemId, out int count) ? count : 0,
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
                downstream.TryGetValue(definition.ItemId, out count) ? count : 0,
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
                integrityFailures,
                historicalBeforeValues);
        }
    }

    private static void CaptureItemMarketMetrics(
        ItemDefinitionSO definition,
        float beforeEwu,
        V27ItemValue value,
        string path,
        string sourceDigest,
        BalanceCaptureFactory capture,
        ICollection<string> integrityFailures,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        const string AppraisedValuablesId = "offense:appraised-valuables";
        bool appraised = string.Equals(
            definition.ItemId,
            AppraisedValuablesId,
            StringComparison.Ordinal);
        int beforeUnitPrice = ResolveV23MarketUnitPrice(
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
        if (currentUnitPrice != beforeUnitPrice && currentUnitPrice != afterUnitPrice)
        {
            integrityFailures.Add(
                $"V27 unit price authority drift: {definition.ItemId}; "
                + $"V23={beforeUnitPrice}, V27={afterUnitPrice}, current={currentUnitPrice}.");
        }
        string stableId = RawStableId(definition, "itemId");
        string dependencyFingerprint = HashText(value.SelectedSourceId);
        string approvalSourceDigest = definition is ResourceItemDefinitionSO
            ? GetApprovalSourceDigest(path, "unitPrice", "saleRate")
            : GetApprovalSourceDigest(path, "unitPrice");
        string beforeToken = beforeUnitPrice.ToString(CultureInfo.InvariantCulture);
        string afterToken = afterUnitPrice.ToString(CultureInfo.InvariantCulture);
        const string priceReason = "v27-market-acquisition-input-ceil";
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = ResolveDomain(path, definition.GetType().Name),
            DefinitionKind = "item-market",
            StableId = stableId,
            Metric = "authored-unit-price-gold",
            Unit = "gold",
            Before = beforeToken,
            After = afterToken,
            AuthoredRoundedValue = afterToken,
            PercentDelta = Token(PercentDelta(beforeUnitPrice, afterUnitPrice)),
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
            UpstreamOnlyAfter = afterToken,
            InheritedDelta = checked(afterUnitPrice - beforeUnitPrice).ToString(
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
            ReviewStatus = currentUnitPrice == afterUnitPrice ? "implemented" : "pending",
            ApprovalKey = beforeUnitPrice == afterUnitPrice
                ? string.Empty
                : BuildApprovalKey(
                    stableId,
                    "authored-unit-price-gold",
                    afterToken,
                    dependencyFingerprint,
                    approvalSourceDigest,
                    priceReason,
                    MarketBaselineRecordId),
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = HashText(
                definition.ItemId + "|unitPrice|" + value.SelectedSourceId),
            SourceDigest = approvalSourceDigest,
            SemanticHash = HashText(
                definition.ItemId + "|authored-unit-price-gold|" + afterToken),
            AssetApplied = currentUnitPrice == afterUnitPrice ? "true" : "false",
            BalanceBaselineRecordId = MarketBaselineRecordId
        });

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
                integrityFailures,
                historicalBeforeValues);
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
        ICollection<string> integrityFailures,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
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
        if (!hasHistoricalBefore
            && currentRate != afterRate
            && !AreSameOrAdjacentNonNegativeFloats(currentRate, formulaBeforeRate))
        {
            integrityFailures.Add(
                $"V27 V23 sale-rate reconstruction drift exceeds one float ULP: "
                + $"{definition.ItemId}; formula={formulaBeforeToken}, current={currentToken}.");
        }
        string beforeToken = hasHistoricalBefore
            ? historicalBeforeToken
            : currentRate != afterRate
                ? currentToken
                : formulaBeforeToken;
        float beforeRate = float.Parse(
            beforeToken,
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        if (currentRate != beforeRate && currentRate != afterRate)
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
        const string reason = "v27-market-sale-rate-output-floor";
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = ResolveDomain(path, definition.GetType().Name),
            DefinitionKind = "item-market",
            StableId = stableId,
            Metric = "authored-market-sale-rate",
            Unit = "ratio",
            Before = beforeToken,
            After = afterToken,
            AuthoredRoundedValue = afterToken,
            PercentDelta = Token(PercentDelta(
                (decimal)beforeRate,
                (decimal)afterRate)),
            ExactFormula = "max float rate where floor(unitPrice*rate*3000mEWU)<=floor(AcquisitionCost*0.60)",
            BeforeBom = "N/A",
            AfterBom = "N/A",
            BeforeDirectWu = "N/A",
            AfterDirectWu = "N/A",
            BeforeBomEwu = "N/A",
            AfterBomEwu = "N/A",
            BeforeLaborDensity = "N/A",
            AfterLaborDensity = "N/A",
            UpstreamOnlyAfter = afterToken,
            InheritedDelta = Token((decimal)afterRate - (decimal)beforeRate),
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
            ReviewStatus = currentRate == afterRate ? "implemented" : "pending",
            ApprovalKey = beforeRate == afterRate
                ? string.Empty
                : BuildApprovalKey(
                    stableId,
                    "authored-market-sale-rate",
                    afterToken,
                    dependencyFingerprint,
                    approvalSourceDigest,
                    reason,
                    MarketBaselineRecordId),
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = HashText(
                definition.ItemId + "|saleRate|" + beforeToken),
            SourceDigest = approvalSourceDigest,
            SemanticHash = HashText(
                definition.ItemId + "|authored-market-sale-rate|" + afterToken),
            AssetApplied = currentRate == afterRate ? "true" : "false",
            BalanceBaselineRecordId = MarketBaselineRecordId
        });

        long beforeCredit = checked((long)Math.Floor(
            beforeUnitPrice * (decimal)beforeRate * 3000m));
        long afterCredit = checked((long)Math.Floor(
            afterUnitPrice * (decimal)afterRate * 3000m));
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
            Before = beforeCredit.ToString(CultureInfo.InvariantCulture),
            After = afterCredit.ToString(CultureInfo.InvariantCulture),
            AuthoredRoundedValue = afterCredit.ToString(CultureInfo.InvariantCulture),
            PercentDelta = Token(PercentDelta(beforeCredit, afterCredit)),
            ExactFormula = "floor(authoredUnitPrice*authoredSaleRate*3000mEWU)",
            BeforeBom = "N/A",
            AfterBom = "N/A",
            BeforeDirectWu = "N/A",
            AfterDirectWu = "N/A",
            BeforeBomEwu = "N/A",
            AfterBomEwu = "N/A",
            BeforeLaborDensity = "N/A",
            AfterLaborDensity = "N/A",
            UpstreamOnlyAfter = afterCredit.ToString(CultureInfo.InvariantCulture),
            InheritedDelta = checked(afterCredit - beforeCredit).ToString(
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
            ReviewStatus = creditExceedsTarget ? "blocked" : "review",
            ApprovalKey = string.Empty,
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = HashText(
                definition.ItemId + "|market-sale-credit|" + beforeToken),
            SourceDigest = approvalSourceDigest,
            SemanticHash = HashText(
                definition.ItemId + "|market-sale-credit|" + afterCredit),
            AssetApplied = "true",
            BalanceBaselineRecordId = MarketBaselineRecordId
        });
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
        IDictionary<string, string> sourceDigests,
        ICollection<string> integrityFailures,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
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
            beforePrices[item.ItemId] = ResolveV23MarketUnitPrice(item.ItemId, beforeEwu);
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
                integrityFailures,
                path,
                GetApprovalSerializedDigest(saleItem, "cost"),
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
                "SaleItem.cost->FacilityShop purchase debit");
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
            if (!hasHistorical
                && stock.dailyUnitCost != afterCost
                && !AreSameOrAdjacentNonNegativeFloats(stock.dailyUnitCost, formulaBefore))
            {
                integrityFailures.Add(
                    $"V27 stock V23 cost reconstruction drift exceeds one float ULP: "
                    + $"{stock.id}; formula={formulaBeforeToken}, current={currentToken}.");
            }
            string beforeToken = hasHistorical
                ? historicalToken
                : stock.dailyUnitCost != afterCost
                    ? currentToken
                    : formulaBeforeToken;
            CaptureMarketConsumerPatch(
                capture,
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
                "AuthoredStockCategoryRecord.dailyUnitCost->StockSupplyService purchase debit");
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
                "GuestRequestDefinitionSO.successEffects(Money)->campaign reward credit");
        }
    }

    private static void CaptureMarketConsumerPatch(
        BalanceCaptureFactory capture,
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
        string executionRoute)
    {
        if (!string.Equals(current, before, StringComparison.Ordinal)
            && !string.Equals(current, after, StringComparison.Ordinal))
        {
            integrityFailures.Add(
                $"V27 market consumer authority drift: {stableId}:{metric}; "
                + $"Before={before}, After={after}, current={current}.");
        }
        decimal beforeNumber = decimal.Parse(before, NumberStyles.Float, CultureInfo.InvariantCulture);
        decimal afterNumber = decimal.Parse(after, NumberStyles.Float, CultureInfo.InvariantCulture);
        string dependencyFingerprint = HashText(dependencyId);
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = ResolveDomain(path, definitionKind),
            DefinitionKind = definitionKind,
            StableId = stableId,
            Metric = metric,
            Unit = unit,
            Before = before,
            After = after,
            AuthoredRoundedValue = after,
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
            UpstreamOnlyAfter = after,
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
            ReviewStatus = string.Equals(current, after, StringComparison.Ordinal)
                ? "implemented"
                : "pending",
            ApprovalKey = string.Equals(before, after, StringComparison.Ordinal)
                ? string.Empty
                : BuildApprovalKey(
                    stableId,
                    metric,
                    after,
                    dependencyFingerprint,
                    approvalSourceDigest,
                    reasonCode,
                    MarketBaselineRecordId),
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = HashText(stableId + "|" + metric + "|" + dependencyId),
            SourceDigest = approvalSourceDigest,
            SemanticHash = HashText(stableId + "|" + metric + "|" + after),
            AssetApplied = string.Equals(current, after, StringComparison.Ordinal)
                ? "true"
                : "false",
            BalanceBaselineRecordId = MarketBaselineRecordId
        });
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
        int downstream,
        string path,
        string sourceDigest,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies)
    {
        decimal percent = PercentDelta(before, after);
        BalanceAnomalySeverity severity = BalanceAnomalyDetector.ClassifyPercentDelta(
            Math.Abs(percent));
        if (definition is ResourceItemDefinitionSO resource
            && resource.Kind == ResourceItemKind.Raw)
        {
            severity = Max(severity, BalanceAnomalyDetector.ClassifyPrimitiveDelta(
                Math.Abs(percent), downstream));
        }
        string[] dependencies = selectedSourceId.StartsWith("external:", StringComparison.Ordinal)
            ? Array.Empty<string>()
            : new[] { selectedSourceId };
        string[] rootCauseIds = metric == "recoverable-value"
            ? dependencies.Length > 0 ? dependencies : new[] { definition.ItemId }
            : dependencies;
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
        string stableId = RawStableId(definition, "itemId");
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
                definition.ItemId,
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
        decimal after = decimal.Ceiling(before * LaborScale);
        if (current != before && current != after)
        {
            throw new InvalidOperationException(
                $"Crop authored work drifted outside its V27 Before/After contract: "
                + $"{cropId}:{propertyPath}; current={Token(current)}, "
                + $"before={Token(before)}, after={Token(after)}.");
        }
        string afterToken = Token(after);
        string approvalSourceDigest = GetApprovalSourceDigest(
            path,
            "sowWork",
            "harvestWork");
        BalanceAnomalySeverity severity = BalanceAnomalyDetector.ClassifyPercentDelta(
            Math.Abs(PercentDelta(before, after)));
        const string reasonCode = "v27-crop-work-duration-preserving";
        capture.Capture(new BalanceMetricCaptureRequest
        {
            Domain = "agriculture",
            DefinitionKind = "crop",
            StableId = cropId,
            Metric = metric,
            Unit = "WU",
            Before = Token(before),
            After = afterToken,
            AuthoredRoundedValue = afterToken,
            PercentDelta = Token(PercentDelta(before, after)),
            ExactFormula = "ceil(authored crop work*2.25)",
            BeforeBom = bom,
            AfterBom = bom,
            BeforeDirectWu = Token(before),
            AfterDirectWu = afterToken,
            BeforeBomEwu = "see:cultivated-acquisition-cost",
            AfterBomEwu = "see:cultivated-acquisition-cost",
            BeforeLaborDensity = "see:cultivated-acquisition-cost",
            AfterLaborDensity = "see:cultivated-acquisition-cost",
            UpstreamOnlyAfter = Token(before),
            InheritedDelta = "0",
            RawLocalDelta = Token(after - before),
            LocalQuantizationBoundaryCount = 1,
            DownstreamConsumerCount = "1",
            DependencyIds = dependencies,
            RootCauseIds = Array.Empty<string>(),
            AnomalyDisposition = severity == BalanceAnomalySeverity.Critical
                ? "local-critical"
                : severity == BalanceAnomalySeverity.Warning ? "warning" : "none",
            ReasonCode = reasonCode,
            ReasonDetail = "Authored crop work candidate; explicit exact approval required before apply.",
            SourceAuthority = path,
            SourcePropertyPath = propertyPath,
            ExecutionRoute = "CropDefinitionSO->CropPlotRuntime->AIWork",
            SaveAuthority = "CropDefinitionSO",
            VerificationEvidence = "V27 crop audit; PlayMode evidence pending",
            ReviewStatus = "pending",
            ApprovalKey = before != after
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
        foreach (ProductionRecipeSO recipe in recipes)
        {
            if (!before.Recipes.TryGetValue(recipe.RecipeId, out EmbeddedWorkValueRecipeBreakdown beforeValue)
                || !after.Recipes.TryGetValue(recipe.RecipeId, out V27RecipeValueBreakdown afterValue))
            {
                continue;
            }
            string path = AssetDatabase.GetAssetPath(recipe);
            string sourceDigest = GetSourceDigest(path, sourceDigests);
            decimal beforeWu = BalanceCanonicalText.DecimalFromFiniteFloat(
                beforeValue.DirectWork,
                $"recipe:{recipe.RecipeId}:beforeDirectWU");
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
            decimal beforeDensity = comparableBeforeInput > 0m
                ? beforeWu / comparableBeforeInput
                : 0m;
            decimal afterDensity = afterValue.InputDebit.MilliEwu > 0L
                ? afterWu / (afterValue.InputDebit.MilliEwu / 1000m)
                : 0m;
            decimal densityRatio = beforeDensity > 0m && afterDensity > 0m
                ? afterDensity / beforeDensity
                : 1m;
            BalanceAnomalySeverity densitySeverity =
                BalanceAnomalyDetector.ClassifyLaborDensity(densityRatio);
            BalanceAnomalySeverity severity = Max(percentSeverity, densitySeverity);
            string bom = FormatBom(recipe.Inputs);
            string[] dependencies = recipe.Inputs.Select(value => value.ItemId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string localFingerprint = HashText(
                recipe.RecipeId + "|" + bom + "|" + recipe.Outputs.Count + "|"
                + recipe.RequiredWork.ToString("R", CultureInfo.InvariantCulture));
            string stableId = RawStableId(recipe, "recipeId");
            string dependencyFingerprint = HashText(string.Join("|", dependencies));
            string afterToken = Token(afterWu);
            const string reasonCode = "v27-duration-preserving-first-candidate";
            capture.Capture(new BalanceMetricCaptureRequest
            {
                Domain = "production",
                DefinitionKind = "recipe",
                StableId = stableId,
                Metric = "direct-wu",
                Unit = "WU",
                Before = Token(beforeWu),
                After = afterToken,
                AuthoredRoundedValue = Token(decimal.Ceiling(beforeWu * LaborScale)),
                PercentDelta = Token(percent),
                ExactFormula = "ceil(V23 directWU*2.25) at input-debit boundary",
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
                AnomalyDisposition = severity == BalanceAnomalySeverity.Critical
                    ? "root-critical"
                    : severity == BalanceAnomalySeverity.Warning ? "warning" : "none",
                ReasonCode = reasonCode,
                ReasonDetail = "Candidate only; same-route labor-density ratio="
                    + Token(densityRatio)
                    + "; crop inputs use cultivated Before acquisition while V23 item rows remain frozen.",
                SourceAuthority = path,
                SourcePropertyPath = "derived:V23BalanceWorkCalculator.CalculateRecipe",
                ExecutionRoute = "ProductionRecipeSO->ProductionBillRuntime->AIWork",
                SaveAuthority = "ProductionRecipeSO",
                VerificationEvidence = "V27 recipe graph audit",
                ReviewStatus = severity == BalanceAnomalySeverity.Critical ? "pending" : "review",
                ApprovalKey = string.Empty,
                DependencyFingerprint = dependencyFingerprint,
                LocalFingerprint = localFingerprint,
                SourceDigest = sourceDigest,
                SemanticHash = HashText(recipe.RecipeId + "|direct-wu|" + Token(afterWu)),
                AssetApplied = "false",
                BalanceBaselineRecordId = ResolveBaselineRecordId(stableId)
            });
            decimal authoredCurrent = BalanceCanonicalText.DecimalFromFiniteFloat(
                recipe.RequiredWork,
                $"recipe:{recipe.RecipeId}:requiredWork");
            decimal authoredBefore = ResolveHistoricalAuthoredBefore(
                stableId,
                "authored-required-wu",
                beforeWu,
                historicalBeforeValues);
            decimal authoredAfter = decimal.Ceiling(authoredBefore * LaborScale);
            if (authoredCurrent != authoredBefore && authoredCurrent != authoredAfter)
            {
                throw new InvalidOperationException(
                    $"Recipe authored work drifted outside its V27 Before/After contract: "
                    + $"{recipe.RecipeId}; current={Token(authoredCurrent)}, "
                    + $"before={Token(authoredBefore)}, after={Token(authoredAfter)}.");
            }
            string authoredAfterToken = Token(authoredAfter);
            string approvalSourceDigest = GetApprovalSourceDigest(path, "requiredWork");
            string authoredFingerprint = HashText(
                recipe.RecipeId + "|requiredWork|" + Token(authoredBefore));
            capture.Capture(new BalanceMetricCaptureRequest
            {
                Domain = "production",
                DefinitionKind = "recipe",
                StableId = stableId,
                Metric = "authored-required-wu",
                Unit = "WU",
                Before = Token(authoredBefore),
                After = authoredAfterToken,
                AuthoredRoundedValue = authoredAfterToken,
                PercentDelta = Token(PercentDelta(authoredBefore, authoredAfter)),
                ExactFormula = "ceil(authored requiredWork*2.25)",
                BeforeBom = bom,
                AfterBom = bom,
                BeforeDirectWu = Token(authoredBefore),
                AfterDirectWu = authoredAfterToken,
                BeforeBomEwu = Token(comparableBeforeInput),
                AfterBomEwu = afterValue.InputDebit.ToCanonicalEwuToken(),
                BeforeLaborDensity = comparableBeforeInput > 0m
                    ? Token(authoredBefore / comparableBeforeInput)
                    : "N/A",
                AfterLaborDensity = afterValue.InputDebit.MilliEwu > 0L
                    ? Token(authoredAfter / (afterValue.InputDebit.MilliEwu / 1000m))
                    : "N/A",
                UpstreamOnlyAfter = Token(authoredBefore),
                InheritedDelta = "0",
                RawLocalDelta = Token(authoredAfter - authoredBefore),
                LocalQuantizationBoundaryCount = 1,
                DownstreamConsumerCount = recipe.Outputs.Count.ToString(CultureInfo.InvariantCulture),
                DependencyIds = dependencies,
                RootCauseIds = Array.Empty<string>(),
                AnomalyDisposition = "warning",
                ReasonCode = reasonCode,
                ReasonDetail = "Authored integer display candidate; runtime work authority is "
                    + "V27BalanceWorkCalculator. Same-route labor-density ratio="
                    + Token(densityRatio) + "; explicit exact approval required.",
                SourceAuthority = path,
                SourcePropertyPath = "requiredWork",
                ExecutionRoute = "ProductionRecipeSO authored display + V27BalanceWorkCalculator->ProductionBillRuntime->AIWork",
                SaveAuthority = "ProductionRecipeSO authored display + V27 runtime formula",
                VerificationEvidence = "V27 authored work audit",
                ReviewStatus = "pending",
                ApprovalKey = authoredBefore != authoredAfter
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
            if (severity != BalanceAnomalySeverity.None)
            {
                anomalies.Add(BalanceAnomalyNode.Capture(
                    recipe.RecipeId,
                    "direct-wu",
                    severity,
                    severity == BalanceAnomalySeverity.Critical
                        ? BalanceAnomalyDisposition.RootCritical
                        : BalanceAnomalyDisposition.None,
                    densitySeverity > percentSeverity
                        ? "labor-density-drift"
                        : "v27-duration-preserving-first-candidate",
                    Array.Empty<string>()));
            }
        }
    }

    private static void CaptureBuildingCandidates(
        IEnumerable<BuildingSO> definitions,
        EmbeddedWorkValueSnapshot beforeValues,
        V27EmbeddedWorkValueSnapshot afterValues,
        IBalanceWorkCalculator work,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        foreach (BuildingSO building in definitions
                     .Where(value => value != null && value.id >= 0 && !value.IsDeprecatedCompatibilityAsset)
                     .OrderBy(value => value.ContentDefinitionId, StringComparer.Ordinal))
        {
            string stableId = ResolveBuildingStableId(building);
            IReadOnlyList<ItemAmountDefinition> materials = building.GetConstructionMaterials();
            if (materials.Count == 0)
                continue;
            decimal beforeWu = BalanceCanonicalText.DecimalFromFiniteFloat(
                work.CalculateConstruction(building),
                $"building:{stableId}:constructionWU");
            decimal afterWu = decimal.Ceiling(beforeWu * LaborScale);
            decimal candidateRedistributedWu = decimal.Ceiling(beforeWu * 1.5m);
            decimal beforeBomEwu = 0m;
            long afterBomMilli = 0L;
            bool resolved = true;
            foreach (ItemAmountDefinition material in materials)
            {
                if (!beforeValues.TryGetItemWork(material.ItemId, out float oldWork)
                    || !afterValues.Items.TryGetValue(material.ItemId, out V27ItemValue newValue))
                {
                    resolved = false;
                    break;
                }
                beforeBomEwu += BalanceCanonicalText.DecimalFromFiniteFloat(
                    oldWork,
                    $"building:{stableId}:bom") * material.Amount;
                afterBomMilli = checked(
                    afterBomMilli + newValue.AcquisitionCost.MilliEwu * material.Amount);
            }
            if (!resolved || beforeBomEwu <= 0m || afterBomMilli <= 0L)
                continue;
            decimal afterBomEwu = afterBomMilli / 1000m;
            decimal beforeDensity = beforeWu / beforeBomEwu;
            decimal afterDensity = afterWu / afterBomEwu;
            decimal densityRatio = beforeDensity == 0m ? 0m : afterDensity / beforeDensity;
            BalanceAnomalySeverity densitySeverity =
                BalanceAnomalyDetector.ClassifyLaborDensity(densityRatio);
            string path = AssetDatabase.GetAssetPath(building);
            string sourceDigest = GetSourceDigest(path, sourceDigests);
            string bom = FormatBom(materials);
            string[] dependencies = materials.Select(value => value.ItemId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            BuildingWorkAmountAbility authoredWork =
                building.GetAbility<BuildingWorkAmountAbility>();
            string authoredWorkPath = authoredWork != null
                ? FindUniqueSerializedPropertyPath(building, "constructionWorkRequired")
                : string.Empty;
            CaptureBuildingCandidate(
                capture, building, stableId, path, sourceDigest, dependencies,
                "construction-wu:period-preserving", beforeWu, afterWu, bom,
                beforeBomEwu, afterBomEwu, beforeDensity, afterDensity,
                densitySeverity, "WU*2.25; BOM unchanged", false,
                "derived:V23BalanceWorkCalculator.CalculateConstruction");
            CaptureBuildingCandidate(
                capture, building, stableId, path, sourceDigest, dependencies,
                "construction-wu:bom-redistribution", beforeWu, candidateRedistributedWu,
                bom, beforeBomEwu, afterBomEwu, beforeDensity,
                candidateRedistributedWu / afterBomEwu,
                BalanceAnomalySeverity.Warning,
                "REJECTED: WU*1.5 plus any BOM increase lowers labor density; "
                + "the period-preserving unchanged-BOM candidate is the bounded minimum change.",
                false,
                "derived:V23BalanceWorkCalculator.CalculateConstruction");
            CaptureBuildingCandidate(
                capture, building, stableId, path, sourceDigest, dependencies,
                "construction-wu:min-change", beforeWu, afterWu, bom,
                beforeBomEwu, afterBomEwu, beforeDensity, afterDensity,
                densitySeverity,
                "SELECTED: exact 2.25 labor scale with unchanged BOM preserves period and density.",
                false,
                "derived:V23BalanceWorkCalculator.CalculateConstruction");
            CaptureBuildingCandidate(
                capture, building, stableId, path, sourceDigest, dependencies,
                "construction-wu:approved", beforeWu, afterWu, bom,
                beforeBomEwu, afterBomEwu, beforeDensity, afterDensity,
                densitySeverity,
                "SELECTED: V27 runtime work authority uses the exact 45/20 scale.",
                false,
                "derived:V23BalanceWorkCalculator.CalculateConstruction");
            if (authoredWork != null)
            {
                decimal authoredCurrent = BalanceCanonicalText.DecimalFromFiniteFloat(
                    authoredWork.constructionWorkRequired,
                    $"building:{stableId}:constructionWorkRequired");
                decimal authoredBefore = ResolveBuildingAuthoredBefore(
                    stableId,
                    authoredCurrent,
                    historicalBeforeValues);
                decimal authoredPeriod = decimal.Ceiling(authoredBefore * LaborScale);
                if (authoredCurrent != authoredBefore && authoredCurrent != authoredPeriod)
                {
                    throw new InvalidOperationException(
                        $"Building authored work drifted outside its V27 Before/After contract: "
                        + $"{stableId}; current={Token(authoredCurrent)}, "
                        + $"before={Token(authoredBefore)}, after={Token(authoredPeriod)}.");
                }
                decimal authoredRedistributed = decimal.Ceiling(authoredBefore * 1.5m);
                CaptureBuildingCandidate(
                    capture, building, stableId, path, sourceDigest, dependencies,
                    "construction-authored-wu:period-preserving",
                    authoredBefore, authoredPeriod, bom,
                    beforeBomEwu, afterBomEwu,
                    authoredBefore / beforeBomEwu,
                    authoredPeriod / afterBomEwu,
                    BalanceAnomalyDetector.ClassifyLaborDensity(
                        (authoredPeriod / afterBomEwu) / (authoredBefore / beforeBomEwu)),
                    "ceil(authored constructionWorkRequired*2.25); BOM unchanged",
                    true, authoredWorkPath,
                    authoredCurrent == authoredPeriod);
                CaptureBuildingCandidate(
                    capture, building, stableId, path, sourceDigest, dependencies,
                    "construction-authored-wu:bom-redistribution",
                    authoredBefore, authoredRedistributed, bom,
                    beforeBomEwu, afterBomEwu,
                    authoredBefore / beforeBomEwu,
                    authoredRedistributed / afterBomEwu,
                    BalanceAnomalySeverity.Warning,
                    "REJECTED: ceil(authored constructionWorkRequired*1.5) would lower "
                    + "labor density and adding BOM would worsen the denominator.",
                    false, authoredWorkPath);
                CaptureBuildingCandidate(
                    capture, building, stableId, path, sourceDigest, dependencies,
                    "construction-authored-wu:approved",
                    authoredBefore, authoredPeriod, bom,
                    beforeBomEwu, afterBomEwu,
                    authoredBefore / beforeBomEwu,
                    authoredPeriod / afterBomEwu,
                    BalanceAnomalyDetector.ClassifyLaborDensity(
                        (authoredPeriod / afterBomEwu) / (authoredBefore / beforeBomEwu)),
                    "SELECTED: authored integer display mirrors the period-preserving candidate.",
                    false, authoredWorkPath);
            }
            if (densitySeverity != BalanceAnomalySeverity.None)
            {
                anomalies.Add(BalanceAnomalyNode.Capture(
                    stableId,
                    "labor-density",
                    densitySeverity,
                    densitySeverity == BalanceAnomalySeverity.Critical
                        ? BalanceAnomalyDisposition.LocalCritical
                        : BalanceAnomalyDisposition.None,
                    "labor-density-drift",
                    dependencies));
            }
        }
    }

    private static void CaptureDismantleCycles(
        IEnumerable<BuildingSO> definitions,
        EmbeddedWorkValueSnapshot beforeValues,
        V27EmbeddedWorkValueSnapshot afterValues,
        IBalanceWorkCalculator work,
        IMaterialSalvageCalculator salvage,
        BalanceCaptureFactory capture,
        ICollection<BalanceAnomalyNode> anomalies,
        IDictionary<string, string> sourceDigests)
    {
        foreach (BuildingSO building in definitions
                     .Where(value => value != null
                         && value.id >= 0
                         && !value.IsDeprecatedCompatibilityAsset)
                     .OrderBy(value => value.ContentDefinitionId, StringComparer.Ordinal))
        {
            ItemAmountDefinition[] materials = building.GetConstructionMaterials()
                .Where(value => value != null && value.Amount > 0)
                .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .ToArray();
            if (materials.Length == 0
                || materials.Any(value => !beforeValues.ItemWork.ContainsKey(value.ItemId))
                || materials.Any(value => !afterValues.Items.ContainsKey(value.ItemId)))
            {
                continue;
            }

            string stableId = ResolveBuildingStableId(building);
            decimal beforeConstruction = BalanceCanonicalText.DecimalFromFiniteFloat(
                work.CalculateConstruction(building),
                $"building:{stableId}:dismantle-construction");
            decimal afterConstruction = decimal.Ceiling(beforeConstruction * LaborScale);
            MaterialSalvageResult recovered = salvage.Calculate(
                ResolveDismantleKindForAudit(building),
                (float)beforeConstruction,
                materials,
                100f);
            decimal beforeDismantle = BalanceCanonicalText.DecimalFromFiniteFloat(
                recovered.RequiredWork,
                $"building:{stableId}:dismantle-work");
            decimal afterDismantle = decimal.Ceiling(beforeDismantle * LaborScale);
            long beforeBom = 0L;
            long afterBom = 0L;
            foreach (ItemAmountDefinition material in materials)
            {
                beforeBom = checked(beforeBom + V27EwuQuantizer.QuantizeInputDebit(
                    BalanceCanonicalText.DecimalFromFiniteFloat(
                        beforeValues.ItemWork[material.ItemId],
                        $"building:{stableId}:before-bom:{material.ItemId}")
                    * material.Amount).MilliEwu);
                afterBom = checked(afterBom
                    + afterValues.Items[material.ItemId].AcquisitionCost.MilliEwu
                    * material.Amount);
            }

            long beforeRecovered = 0L;
            long afterRecoveredAcquisition = 0L;
            long afterRecoveredCredit = 0L;
            foreach (ItemAmountDefinition material in recovered.RecoveredMaterials)
            {
                beforeRecovered = checked(beforeRecovered + V27EwuQuantizer.QuantizeOutputCredit(
                    BalanceCanonicalText.DecimalFromFiniteFloat(
                        beforeValues.ItemWork[material.ItemId],
                        $"building:{stableId}:before-recovery:{material.ItemId}")
                    * material.Amount).MilliEwu);
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
            string[] dependencies = materials.Select(value => value.ItemId).ToArray();
            string dependencyFingerprint = HashText(string.Join("|", dependencies));
            string beforeToken = beforeMargin.ToString(CultureInfo.InvariantCulture);
            string afterToken = afterMargin.ToString(CultureInfo.InvariantCulture);
            string recoveredBom = FormatBom(recovered.RecoveredMaterials);
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
                BeforeBom = FormatBom(materials),
                AfterBom = FormatBom(materials),
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
                    stableId + "|" + FormatBom(materials) + "|" + recoveredBom),
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
                ExactFormula = "ceil(V23 dismantle requiredWork*2.25)",
                BeforeBom = recoveredBom,
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
                ReasonDetail = "Applied runtime authority; recovery quantities remain V23 and labor is scaled exactly once.",
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
        IBalanceWorkCalculator work,
        IMaterialSalvageCalculator salvage)
    {
        foreach (BuildingSO building in definitions
                     .Where(value => value != null
                         && value.id >= 0
                         && !value.IsDeprecatedCompatibilityAsset)
                     .OrderBy(value => value.ContentDefinitionId, StringComparer.Ordinal))
        {
            ItemAmountDefinition[] materials = building.GetConstructionMaterials()
                .Where(value => value != null && value.Amount > 0)
                .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .ToArray();
            if (materials.Length == 0
                || materials.Any(value => !afterValues.Items.ContainsKey(value.ItemId)))
            {
                continue;
            }
            decimal construction = decimal.Ceiling(
                BalanceCanonicalText.DecimalFromFiniteFloat(
                    work.CalculateConstruction(building),
                    $"building:{building.id}:scc-construction") * LaborScale);
            MaterialSalvageResult recovered = salvage.Calculate(
                ResolveDismantleKindForAudit(building),
                work.CalculateConstruction(building),
                materials,
                100f);
            if (recovered.RecoveredMaterials.Count == 0)
            {
                // A zero-output dismantle is a strict sink. It cannot participate in
                // a positive cycle and BalanceTransform intentionally requires at
                // least one output node. The exhaustive ledger still records it.
                continue;
            }
            decimal dismantle = decimal.Ceiling(
                BalanceCanonicalText.DecimalFromFiniteFloat(
                    recovered.RequiredWork,
                    $"building:{building.id}:scc-dismantle") * LaborScale);
            long debit = checked(
                materials.Sum(value => checked(
                    afterValues.Items[value.ItemId].AcquisitionCost.MilliEwu * value.Amount))
                + V27EwuQuantizer.QuantizeInputDebit(construction).MilliEwu
                + V27EwuQuantizer.QuantizeInputDebit(dismantle).MilliEwu);
            long credit = recovered.RecoveredMaterials.Sum(value => checked(
                afterValues.Items[value.ItemId].RecoverableValue.MilliEwu * value.Amount));
            yield return BalanceTransform.Capture(
                "dismantle:" + ResolveBuildingStableId(building),
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
        string bom,
        decimal beforeBomEwu,
        decimal afterBomEwu,
        decimal beforeDensity,
        decimal afterDensity,
        BalanceAnomalySeverity severity,
        string reason,
        bool patchable,
        string sourcePropertyPath,
        bool assetApplied = false)
    {
        string fingerprint = HashText(
            stableId + "|" + bom + "|" + building.width + "x" + building.height);
        string dependencyFingerprint = HashText(string.Join("|", dependencies));
        string afterToken = Token(afterWu);
        string approvalSourceDigest = patchable
            ? GetApprovalSourceDigest(
                path,
                sourcePropertyPath.Substring(
                    sourcePropertyPath.LastIndexOf('.') + 1))
            : sourceDigest;
        const string reasonCode = "facility-labor-density-review";
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
            BeforeBom = bom,
            AfterBom = metric.Contains("bom-redistribution", StringComparison.Ordinal)
                ? "optimizer-pending:" + bom : bom,
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
            DownstreamConsumerCount = "facility-runtime",
            DependencyIds = dependencies,
            RootCauseIds = dependencies,
            AnomalyDisposition = metric.Contains("bom-redistribution", StringComparison.Ordinal)
                ? "rejected"
                : severity == BalanceAnomalySeverity.Critical
                    ? "local-critical"
                    : severity == BalanceAnomalySeverity.Warning ? "warning" : "none",
            ReasonCode = reasonCode,
            ReasonDetail = reason,
            SourceAuthority = path,
            SourcePropertyPath = sourcePropertyPath,
            ExecutionRoute = "BuildingSO->ConstructionSite->AIWork",
            SaveAuthority = "BuildingSO",
            VerificationEvidence = "V27 facility candidate audit",
            ReviewStatus = metric.Contains("bom-redistribution", StringComparison.Ordinal)
                ? "rejected"
                : metric.EndsWith(":approved", StringComparison.Ordinal)
                    ? "selected"
                    : patchable ? "pending" : "review",
            ApprovalKey = patchable && beforeWu != afterWu
                ? BuildApprovalKey(stableId, metric, afterToken, dependencyFingerprint,
                    approvalSourceDigest, reasonCode, ResolveLaborBaselineRecordId(stableId))
                : string.Empty,
            DependencyFingerprint = dependencyFingerprint,
            LocalFingerprint = fingerprint,
            SourceDigest = approvalSourceDigest,
            SemanticHash = HashText(stableId + "|" + metric + "|" + Token(afterWu)),
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
        ICollection<BalanceAnomalyNode> anomalies)
    {
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
            CanonicalBalanceMetricRecord record = ledger.Records
                .Where(value => string.Equals(value.StableId, rootId, StringComparison.Ordinal)
                    && value.ApprovalKey.Length > 0)
                .OrderBy(value => value.Metric == "authored-required-wu" ? 0 : 1)
                .ThenBy(value => value.Metric, StringComparer.Ordinal)
                .FirstOrDefault();
            if (record == null)
                throw new InvalidOperationException(
                    $"Collapsed Critical references a root with no approvable ledger row: {rootId}");
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

    private static void WriteManifest(
        Stream stream,
        FrozenBalanceLedger ledger,
        string sourceDigest,
        string csvHash,
        string markdownHash,
        string auditHash,
        string anomalyHash,
        string approvalHash,
        string assetPatchDigest,
        int approvedCount,
        IReadOnlyList<BalanceAnomalyNode> anomalies,
        BalanceSccAuditResult scc,
        IReadOnlyList<string> failures)
    {
        using StreamWriter writer = NewLfWriter(stream);
        writer.Write("{\n");
        WriteJsonProperty(writer, "schemaVersion", "v27.1", true);
        WriteJsonProperty(writer, "generatorVersion", GeneratorVersion, true);
        WriteJsonProperty(writer, "sourceDigest", sourceDigest, true);
        WriteJsonNumber(writer, "rowCount", ledger.Count, true);
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
        WriteJsonProperty(writer, "approvalDigest", approvalHash, true);
        WriteJsonProperty(writer, "assetPatchDigest", assetPatchDigest, true);
        string analyzerSource = ProjectAbsolutePath(
            "Tools/DungeonStory.BalanceAnalyzers/DungeonStoryBalanceAnalyzer.cs");
        string analyzerDll = ProjectAbsolutePath("Assets/Analyzers/DungeonStory.BalanceAnalyzers.dll");
        WriteJsonProperty(writer, "analyzerSourceHash",
            File.Exists(analyzerSource) ? HashFile(analyzerSource) : HashText(string.Empty), true);
        WriteJsonProperty(writer, "analyzerDllHash",
            File.Exists(analyzerDll) ? HashFile(analyzerDll) : HashText(string.Empty), true);
        WriteJsonNumber(writer, "criticalCount",
            anomalies.Count(value => value.EmitsCiAnnotation), true);
        WriteJsonNumber(writer, "collapsedCriticalCount",
            anomalies.Count(value => value.Severity == BalanceAnomalySeverity.Critical
                && !value.EmitsCiAnnotation), true);
        WriteJsonNumber(writer, "approvedCount", approvedCount, true);
        WriteJsonNumber(writer, "sccCount", scc.Components.Count, true);
        WriteJsonNumber(writer, "integrityFailureCount", failures.Count, true);
        writer.Write("  \"balanceBaselineRecordIds\": [");
        V27BalanceJsonSerializer.WriteJsonString(writer, BaselineRecordId);
        writer.Write(',');
        V27BalanceJsonSerializer.WriteJsonString(writer, EvidenceBaselineRecordId);
        writer.Write(',');
        V27BalanceJsonSerializer.WriteJsonString(writer, VerticalSliceBaselineRecordId);
        writer.Write(',');
        V27BalanceJsonSerializer.WriteJsonString(writer, SurvivalOutputBaselineRecordId);
        writer.Write(',');
        V27BalanceJsonSerializer.WriteJsonString(writer, MarketBaselineRecordId);
        writer.Write(',');
        V27BalanceJsonSerializer.WriteJsonString(writer, LaborFacilityBaselineRecordId);
        writer.Write("]\n");
        writer.Write("}\n");
        writer.Flush();
    }

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
            digest = HashFile(ProjectAbsolutePath(path));
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
            "Assets/Scripts/Services/Economy/Editor/V27BalanceLaborFacilityDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/Editor/V27BalanceVerticalSlicePlayModeVerifier.cs",
            "Assets/Scripts/Services/Economy/V27BalanceWorkCalculator.cs",
            "Assets/Scripts/Models/Buildings/Core/StockCategoryCatalog.cs",
            "Assets/Scripts/Services/Buildings/SO/StockInfo.cs",
            "Assets/Scripts/Services/Survival/SurvivalFoodRuntime.cs",
            "Assets/Scripts/Services/Items/GameContentCatalog.cs",
            "Tools/DungeonStory.BalanceAnalyzers/DungeonStoryBalanceAnalyzer.cs",
            ApprovalPath,
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

    private static decimal ResolveBuildingAuthoredBefore(
        string stableId,
        decimal current,
        IReadOnlyDictionary<string, string> historicalBeforeValues)
    {
        decimal historical = ResolveHistoricalAuthoredBefore(
            stableId,
            "construction-authored-wu:period-preserving",
            current,
            historicalBeforeValues);
        if (historical != current || historicalBeforeValues != null)
            return historical;
        return string.Equals(stableId, "building:1002", StringComparison.Ordinal)
            ? 40m
            : current;
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
        if (yamlFieldNames == null || yamlFieldNames.Length == 0)
            throw new ArgumentException("At least one YAML field is required.", nameof(yamlFieldNames));
        string absolutePath = ProjectAbsolutePath(projectRelativePath);
        string[] lines = File.ReadAllText(absolutePath, Encoding.UTF8)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        foreach (string yamlFieldName in yamlFieldNames
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
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
            if (matched != 1)
            {
                throw new InvalidOperationException(
                    $"V27 approval digest requires exactly one YAML scalar '{yamlFieldName}' "
                    + $"in {projectRelativePath}; found {matched}.");
            }
        }
        return HashText(string.Join("\n", lines));
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
        FrozenBalanceLedger ledger,
        int criticalCount,
        int sccCount,
        IReadOnlyList<string> integrityFailures)
    {
        Ledger = ledger;
        CriticalCount = criticalCount;
        SccCount = sccCount;
        IntegrityFailures = integrityFailures;
    }

    public FrozenBalanceLedger Ledger { get; }
    public int CriticalCount { get; }
    public int SccCount { get; }
    public IReadOnlyList<string> IntegrityFailures { get; }
}
#endif
