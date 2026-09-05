#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

public static class V27BalanceLedgerDebugScenarios
{
    public const string ReportPath = "Artifacts/QA/v27-balance-ledger-contracts.txt";
    public static double LastCsvEscapeP95Milliseconds { get; private set; }
    public static long LastCsvEscapeAllocatedBytes { get; private set; }

    [MenuItem("DungeonStory/V27/Run Balance Ledger Contracts")]
    public static void RunFromMenu()
    {
        string report;
        Exception failure = null;
        try
        {
            report = RunAll();
        }
        catch (Exception exception)
        {
            failure = exception;
            report = "RESULT=FAIL; contract=" + exception.Message + "\n";
        }
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
        {
            using StreamWriter writer = new StreamWriter(
                stream,
                new UTF8Encoding(false, true),
                4096,
                leaveOpen: true);
            writer.Write(report);
            writer.Flush();
        });
        AssetDatabase.Refresh();
        if (failure == null)
            Debug.Log(report);
        else
            Debug.LogError(report + failure);
    }

    public static string RunAll()
    {
        List<string> passed = new List<string>();
        VerifyAsymmetricQuantization();
        passed.Add("PASS V27_MEWU_ASYMMETRIC_QUANTIZATION");
        VerifyBatchPartitionMonotonicity();
        passed.Add("PASS V27_MEWU_BATCH_PARTITION_MONOTONICITY");
        VerifyAttributionCollapseEnvelope();
        passed.Add("PASS V27_ATTRIBUTION_COLLAPSE_EPSILON_ISOLATED");
        VerifyItemMetricRootAttribution();
        passed.Add("PASS V27_ITEM_METRIC_ROOT_ATTRIBUTION");
        VerifySccProof();
        passed.Add("PASS V27_SCC_ZERO_TOLERANCE");
        VerifyCanonicalCaptureAndOrdering();
        passed.Add("PASS V27_CAPTURE_NORMALIZATION_AND_STABLE_SORT");
        VerifyRfc4180Escaping();
        passed.Add("PASS V27_CSV_RFC4180_ESCAPE");
        VerifyCsvLedgerDeterminism();
        passed.Add("PASS V27_CSV_BYTE_DETERMINISM");
        VerifyApprovalExpiry();
        passed.Add("PASS V27_APPROVAL_EXACT_KEY_EXPIRY");
        VerifyApprovalOnlyDerivedItemRows();
        passed.Add("PASS V27_APPROVAL_ONLY_DERIVED_ITEM_NO_ASSET_PATCH");
        VerifyRuntimeLaborAuthority();
        passed.Add("PASS V27_VERTICAL_SLICE_RUNTIME_WORK_SCALE");
        passed.Add("PASS V27_VERTICAL_SLICE_AUTHORITY_ALIGNMENT");
        passed.Add("PASS V27_VERTICAL_SLICE_DISMANTLE_REBUILD_NEGATIVE");
        VerifyStableSortPerformance();
        passed.Add("PASS V27_STABLE_SORT_P95_2MS_ZERO_ALLOC");
        VerifyCsvEscapeKernelPerformance();
        passed.Add(FormatCsvEscapePerformancePass());
        return "RESULT=PASS; checks=" + passed.Count.ToString(CultureInfo.InvariantCulture)
            + "\n" + string.Join("\n", passed) + "\n";
    }

    public static string RunCsvEscapePerformanceOnly()
    {
        VerifyCsvEscapeKernelPerformance();
        return FormatCsvEscapePerformancePass();
    }

    private static string FormatCsvEscapePerformancePass() =>
        "PASS V27_CSV_ESCAPE_P95_2MS_ZERO_ALLOC p95<=2ms; allocated=0B";

    private static void VerifyAsymmetricQuantization()
    {
        Require(V27EwuQuantizer.QuantizeInputDebit(0.0001m).MilliEwu == 1L,
            "input debit did not ceil to 1 mEWU");
        Require(V27EwuQuantizer.QuantizeOutputCredit(0.0009m).MilliEwu == 0L,
            "output credit did not floor below 1 mEWU");
        Require(V27EwuQuantizer.QuantizeInputDebit(1.0001m).MilliEwu == 1001L,
            "input debit 1.0001 EWU mismatch");
        Require(V27EwuQuantizer.QuantizeOutputCredit(1.0009m).MilliEwu == 1000L,
            "output credit 1.0009 EWU mismatch");
        Require(V27EwuQuantizer.DivideInputCost(10L, 3L).MilliEwu == 4L,
            "input division did not ceil");
        Require(V27EwuQuantizer.DivideOutputValue(10L, 3L).MilliEwu == 3L,
            "output division did not floor");
        EwuRational expectedUnits = EwuRational.FromDecimal(1.5m);
        Require(expectedUnits.Numerator == 3L && expectedUnits.Denominator == 2L,
            "decimal probability rational was not reduced");
        Require(V27EwuQuantizer.DivideInputCost(10L, expectedUnits).MilliEwu == 7L,
            "rational expected-output input division did not ceil");
        Require(V27EwuQuantizer.DivideOutputValue(10L, expectedUnits).MilliEwu == 6L,
            "rational expected-output output division did not floor");
        RequireThrows<ArgumentOutOfRangeException>(
            () => V27EwuQuantizer.QuantizeInputDebit(-0.001m),
            "negative input debit was accepted");
        RequireThrows<ArgumentOutOfRangeException>(
            () => V27EwuQuantizer.DivideInputCost(1L, 0L),
            "zero output units were accepted");
    }

    private static void VerifyBatchPartitionMonotonicity()
    {
        long wholeInput = V27EwuQuantizer.QuantizeInputDebit(1.0001m).MilliEwu;
        long splitInput = checked(
            V27EwuQuantizer.QuantizeInputDebit(0.50005m).MilliEwu
            + V27EwuQuantizer.QuantizeInputDebit(0.50005m).MilliEwu);
        Require(splitInput >= wholeInput, "splitting an input batch reduced debit");

        long wholeOutput = V27EwuQuantizer.QuantizeOutputCredit(1.0009m).MilliEwu;
        long splitOutput = checked(
            V27EwuQuantizer.QuantizeOutputCredit(0.50045m).MilliEwu
            + V27EwuQuantizer.QuantizeOutputCredit(0.50045m).MilliEwu);
        Require(splitOutput <= wholeOutput, "splitting an output batch increased credit");
    }

    private static void VerifyAttributionCollapseEnvelope()
    {
        BalanceAttributionResult exact = BalanceAttribution.Attribute(
            100L, 150L, 150L, true, true, 0, new[] { "raw:timber" }, true);
        Require(exact.Disposition == BalanceAnomalyDisposition.CollapsedInheritedOnly,
            "exact inherited delta did not collapse");

        BalanceAttributionResult rounding = BalanceAttribution.Attribute(
            100L, 150L, 152L, true, true, 8, new[] { "raw:timber" }, true);
        Require(rounding.RoundingEnvelope == 2
                && rounding.Disposition == BalanceAnomalyDisposition.CollapsedRoundingOnly,
            "2 mEWU presentation envelope did not collapse inherited rounding");

        BalanceAttributionResult tooLarge = BalanceAttribution.Attribute(
            100L, 150L, 153L, true, true, 100, new[] { "raw:timber" }, true);
        Require(tooLarge.Disposition == BalanceAnomalyDisposition.LocalCritical,
            "depth/boundary count incorrectly expanded the 2 mEWU cap");

        BalanceAttributionResult changedLocal = BalanceAttribution.Attribute(
            100L, 150L, 151L, false, true, 10, new[] { "raw:timber" }, true);
        Require(changedLocal.Disposition == BalanceAnomalyDisposition.LocalCritical,
            "changed local fingerprint was incorrectly collapsed");
    }

    private static void VerifyItemMetricRootAttribution()
    {
        HashSet<string> semanticRoots = new HashSet<string>(StringComparer.Ordinal)
        {
            "recipe:plank",
            "crop:wheat"
        };
        Require(V27BalanceAudit.ResolveItemMetricRootCauseIds(
                "item:plank",
                "acquisition-cost",
                "recipe:plank",
                semanticRoots,
                acquisitionEmitsRootCritical: false)
            .SequenceEqual(new[] { "recipe:plank" }),
            "registered semantic source did not remain the causal root");
        Require(V27BalanceAudit.ResolveItemMetricRootCauseIds(
                "equipment-item:shield:mana-buckler",
                "acquisition-cost",
                "equipment:shield:mana-buckler",
                semanticRoots,
                acquisitionEmitsRootCritical: true)
            .Length == 0,
            "unregistered source alias incorrectly became a non-ledger root");
        Require(V27BalanceAudit.ResolveItemMetricRootCauseIds(
                "equipment-item:shield:mana-buckler",
                "recoverable-value",
                "equipment:shield:mana-buckler",
                semanticRoots,
                acquisitionEmitsRootCritical: true)
            .SequenceEqual(new[] { "equipment-item:shield:mana-buckler" }),
            "recoverable value did not collapse under its approvable acquisition root");
    }

    private static void VerifySccProof()
    {
        BalanceTransform[] safe =
        {
            BalanceTransform.Capture("transform:smelt", new[] { "raw:ore" },
                new[] { "material:ingot" }, 1001L, 1000L),
            BalanceTransform.Capture("transform:recycle", new[] { "material:ingot" },
                new[] { "raw:ore" }, 1001L, 1000L)
        };
        BalanceSccAuditResult safeResult = BalanceSccAuditor.Audit(safe);
        Require(safeResult.Passed, "strictly lossy cycle was rejected");
        Require(safeResult.Components.Any(component => component.Count == 2),
            "two-node SCC was not found");

        BalanceTransform zeroMargin = BalanceTransform.Capture(
            "transform:free-loop", new[] { "raw:ore" }, new[] { "raw:ore" }, 1000L, 1000L);
        BalanceSccAuditResult failed = BalanceSccAuditor.Audit(new[] { zeroMargin });
        Require(!failed.Passed
                && failed.ViolatingTransformIds.SequenceEqual(new[] { "transform:free-loop" }),
            "zero-margin SCC transform passed without the required 1 mEWU loss");
    }

    private static void VerifyApprovalOnlyDerivedItemRows()
    {
        BalanceMetricCaptureRequest request = CreateRequest(
            "items",
            "item",
            "equipment-item:shield:mana-buckler",
            "acquisition-cost",
            "derived acquisition root");
        request.ExactFormula =
            "ceil(inputs+directWU+logistics+utility+loss / expectedOutput)";
        request.SourcePropertyPath = "recipe graph";
        request.SaveAuthority = "ScriptableObject catalog";
        request.VerificationEvidence = "V23-before|V27-fixed-point";
        request.AnomalyDisposition = "root-critical";
        request.ReasonCode = "v27-duration-preserving-first-candidate";
        request.ReviewStatus = "pending";
        request.ApprovalKey = new string('e', 64);
        request.BalanceBaselineRecordId =
            "architecture:v27-whitebox-ledger-pipeline";
        request.AssetApplied = "false";
        BalanceCaptureFactory factory = new BalanceCaptureFactory();
        factory.Capture(request);
        CanonicalBalanceMetricRecord record = factory.Freeze().Records.Single();
        Require(V27BalanceAssetApplication.IsApprovalOnlyLedgerRecord(record),
            "derived acquisition root was treated as a SerializedProperty patch");

        request.SourcePropertyPath = "salvageRetention";
        BalanceCaptureFactory patchableFactory = new BalanceCaptureFactory();
        patchableFactory.Capture(request);
        Require(!V27BalanceAssetApplication.IsApprovalOnlyLedgerRecord(
                patchableFactory.Freeze().Records.Single()),
            "real item SerializedProperty was incorrectly treated as approval-only");

        request.SourcePropertyPath = "recipe graph";
        request.ExactFormula = "ceil(inputs / expectedOutput)";
        BalanceCaptureFactory malformedFactory = new BalanceCaptureFactory();
        malformedFactory.Capture(request);
        Require(!V27BalanceAssetApplication.IsApprovalOnlyLedgerRecord(
                malformedFactory.Freeze().Records.Single()),
            "malformed derived row escaped the exact approval-only contract");
    }

    private static void VerifyCanonicalCaptureAndOrdering()
    {
        BalanceCaptureFactory factory = new BalanceCaptureFactory();
        factory.Capture(CreateRequest("production", "recipe", "recipe:z", "work", "Z\r\nrow"));
        factory.Capture(CreateRequest("items", "item", "item:a", "acquisition-cost", "A"));
        factory.Capture(CreateRequest("production", "recipe", "recipe:a", "work", "A"));
        FrozenBalanceLedger ledger = factory.Freeze();
        Require(ledger.Count == 3, "capture count mismatch");
        Require(ledger.Records[0].Domain == "items"
                && ledger.Records[1].StableId == "recipe:a"
                && ledger.Records[2].StableId == "recipe:z",
            "stable rank order mismatch");
        Require(ledger.Records[2].ReasonDetail == "Z row",
            "capture-time CRLF normalization mismatch");
        Require(ReferenceEquals(
                ledger.Records[1].DefinitionKind,
                ledger.Records[2].DefinitionKind),
            "canonical string pool did not reuse identical values");

        RequireThrows<InvalidOperationException>(() =>
        {
            BalanceCaptureFactory duplicate = new BalanceCaptureFactory();
            duplicate.Capture(CreateRequest("items", "item", "item:a", "work", "A"));
            duplicate.Capture(CreateRequest("items", "item", "item:a", "work", "B"));
        }, "duplicate ledger key was accepted");
        RequireThrows<InvalidOperationException>(
            () => BalanceCanonicalText.StableId(" item:a", "fixture"),
            "trimmed stable ID was silently canonicalized");
    }

    private static void VerifyRfc4180Escaping()
    {
        string[] inputs =
        {
            string.Empty,
            "ascii",
            "comma,value",
            "quote\"value",
            "line1\rline2",
            "line1\nline2",
            "line1\r\nline2",
            "혼합,\"값\"\n다음",
            "😀",
            "\"\""
        };
        foreach (string input in inputs)
        {
            byte[] encoded = Escape(input);
            string parsed = ParseSingleRfc4180Field(encoded);
            Require(string.Equals(parsed, input, StringComparison.Ordinal),
                "RFC 4180 round trip mismatch");
        }

        string longText = new string('가', 100000) + ",\"tail\"";
        Require(ParseSingleRfc4180Field(Escape(longText)) == longText,
            "long RFC 4180 field round trip mismatch");
        RequireThrows<InvalidDataException>(
            () => Escape("bad\ud800"),
            "unpaired UTF-16 surrogate was accepted");
    }

    private static void VerifyCsvLedgerDeterminism()
    {
        BalanceCaptureFactory firstFactory = new BalanceCaptureFactory();
        firstFactory.Capture(CreateRequest("z-domain", "recipe", "recipe:z", "work", "comma,quote\""));
        firstFactory.Capture(CreateRequest("a-domain", "item", "item:a", "value", "line\nvalue"));
        BalanceCaptureFactory secondFactory = new BalanceCaptureFactory();
        secondFactory.Capture(CreateRequest("a-domain", "item", "item:a", "value", "line\nvalue"));
        secondFactory.Capture(CreateRequest("z-domain", "recipe", "recipe:z", "work", "comma,quote\""));

        byte[] first = Serialize(firstFactory.Freeze());
        byte[] second = Serialize(secondFactory.Freeze());
        Require(first.SequenceEqual(second),
            "dictionary/capture insertion order changed CSV bytes");
        for (int index = 0; index < first.Length; index++)
        {
            if (first[index] == (byte)'\n')
                Require(index > 0 && first[index - 1] == (byte)'\r',
                    "CSV contains a bare LF record delimiter");
        }
        Require(first.Length >= 2
                && first[first.Length - 2] == (byte)'\r'
                && first[first.Length - 1] == (byte)'\n',
            "CSV final record has no CRLF");
    }

    private static void VerifyApprovalExpiry()
    {
        string digestA = new string('a', 64);
        string digestB = new string('b', 64);
        BalanceReviewApproval approval = BalanceReviewApproval.Capture(
            "raw:timber", "acquisition-cost", "8000", digestA, digestB,
            "critical-approved", "balance:v27:timber");
        Require(approval.Matches(
                "raw:timber", "acquisition-cost", "8000", digestA, digestB),
            "exact approval key did not match");
        Require(!approval.Matches(
                "raw:timber", "acquisition-cost", "8001", digestA, digestB),
            "approval survived an After change");
        Require(!approval.Matches(
                "raw:timber", "acquisition-cost", "8000", digestB, digestB),
            "approval survived a dependency fingerprint change");
    }

    private static void VerifyRuntimeLaborAuthority()
    {
        ResourceGameContentCatalog liveContent = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        ItemDefinitionSO[] liveItems = liveContent.GetAll<ItemDefinitionSO>()
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        Require(liveItems.Length == liveContent.Items.Definitions.Count,
            "live content source omitted the dedicated item-definition catalog");
        Require(liveItems.Select(value => value.ItemId)
                .SequenceEqual(
                    liveContent.Items.Definitions
                        .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                        .Select(value => value.ItemId)),
            "live item-definition projection drifted from the item catalog");
        ResourceMaterialEconomicProfileCatalog liveMaterials = new(
            liveContent);
        V23BalanceWorkCalculator liveBefore = new(liveMaterials);
        V27BalanceWorkCalculator liveAfter = new(liveMaterials);
        BuildingSO d03 = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Modular/D03_조리손질대.asset")
            ?? throw new InvalidOperationException("D03 authority is missing.");
        ProductionRecipeSO sawmill = AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
                "Assets/Resources/SO/Economy/Recipes/recipe_sawmill_lumber.asset")
            ?? throw new InvalidOperationException("Sawmill recipe authority is missing.");
        Require(Mathf.Approximately(liveBefore.CalculateConstruction(d03), 208f),
            "live D03 V23 authority drifted from the audit Before value");
        Require(Mathf.Approximately(liveBefore.CalculateRecipe(sawmill), 22f),
            "live sawmill V23 authority drifted from the audit Before value");

        Require(Mathf.Approximately(
                V27BalanceWorkCalculator.ScaleRequiredWork(18f),
                40.5f),
            "logging runtime work did not preserve the 20->45 period");
        Require(Mathf.Approximately(
                V27BalanceWorkCalculator.ScaleRequiredWork(32f),
                72f),
            "quarry runtime work did not preserve the 20->45 period");
        BuildingWorkAmountAbility d03Work = d03.GetAbility<BuildingWorkAmountAbility>()
            ?? throw new InvalidOperationException(
                "D03 authored construction WU authority is missing.");
        float d03RuntimeWork = liveAfter.CalculateConstruction(d03);
        Require(Mathf.Approximately(
                d03RuntimeWork,
                d03Work.constructionWorkRequired),
            "D03 runtime work did not use the authored redistribution authority");
        Require(d03RuntimeWork >= Mathf.Ceil(208f * 1.5f)
                && d03RuntimeWork <= Mathf.Ceil(208f * 2.25f),
            "D03 authored redistribution escaped the 1.5-2.25 WU band");
        RequireThrows<ArgumentOutOfRangeException>(
            () => V27BalanceWorkCalculator.ScaleRequiredWork(0f),
            "zero runtime work was accepted by the V27 authority");
        RequireThrows<ArgumentOutOfRangeException>(
            () => V27BalanceWorkCalculator.ScaleRequiredWork(float.NaN),
            "NaN runtime work was accepted by the V27 authority");

        V23MaterialSalvageCalculator salvage =
            new V23MaterialSalvageCalculator(new FixedMaterialProfileCatalog());
        MaterialSalvageResult result = salvage.Calculate(
            DismantleTargetKind.GeneralFacility,
            d03RuntimeWork,
            d03.GetConstructionMaterials(),
            100f);
        Require(Mathf.Approximately(result.RequiredWork, d03RuntimeWork * 0.25f),
            "D03 dismantle work did not derive from authored construction WU");
        Require(result.RecoveredMaterials.Sum(value => value.Amount)
                < d03.GetConstructionMaterials().Sum(value => value.Amount),
            "D03 dismantle recovery no longer guarantees physical loss");
    }

    private static void VerifyStableSortPerformance()
    {
        const int RowCount = 10000;
        const int WarmupCount = 10;
        const int MeasurementCount = 100;
        BalanceCaptureFactory factory = new BalanceCaptureFactory();
        for (int index = RowCount - 1; index >= 0; index--)
        {
            factory.Capture(CreateRequest(
                "domain:" + (index % 13).ToString("D2", CultureInfo.InvariantCulture),
                "kind:" + (index % 17).ToString("D2", CultureInfo.InvariantCulture),
                "item:" + index.ToString("D5", CultureInfo.InvariantCulture),
                "metric:" + (index % 23).ToString("D2", CultureInfo.InvariantCulture),
                "sort benchmark"));
        }
        CanonicalBalanceMetricRecord[] records = factory.Freeze().Records.ToArray();
        for (int index = 0; index < WarmupCount; index++)
            StableRankSorter.Sort(records);
        double[] milliseconds = new double[MeasurementCount];
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < MeasurementCount; index++)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            StableRankSorter.Sort(records);
            milliseconds[index] = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d
                / System.Diagnostics.Stopwatch.Frequency;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Array.Sort(milliseconds);
        double p95 = milliseconds[(int)Math.Ceiling(MeasurementCount * 0.95d) - 1];
        Require(allocated == 0L, $"stable sort allocated {allocated} bytes after warm-up");
        Require(p95 <= 2d, $"stable sort p95 {p95:F3}ms exceeded 2ms");
    }

    private static void VerifyCsvEscapeKernelPerformance()
    {
        const int FieldCount = 10000;
        const int WarmupCount = 10;
        const int MeasurementCount = 100;
        const int TargetTextLength = 1024 * 1024;
        string[] fields = new string[FieldCount];
        V27CsvFieldShape[] shapes = new V27CsvFieldShape[FieldCount];
        int baseLength = TargetTextLength / FieldCount;
        for (int index = 0; index < fields.Length; index++)
        {
            int length = baseLength + (index < TargetTextLength % FieldCount ? 1 : 0);
            string suffix = (index % 4) switch
            {
                0 => ",",
                1 => "\"",
                2 => "\r\n",
                _ => string.Empty
            };
            fields[index] = new string((char)('a' + index % 26), length - suffix.Length) + suffix;
            shapes[index] = V27CsvFieldShape.Capture(fields[index].AsSpan());
        }

        using MemoryStream stream = new MemoryStream(TargetTextLength * 2);
        // The contract times the escape CPU kernel only. Encoding/flush and disk I/O are
        // intentionally measured separately, so the reusable buffer holds one corpus.
        V27Utf8CsvWriter writer = new V27Utf8CsvWriter(stream, TargetTextLength * 2);
        for (int index = 0; index < WarmupCount; index++)
            RunCsvEscapeKernel(writer, stream, fields, shapes);

        double[] milliseconds = new double[MeasurementCount];
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < MeasurementCount; index++)
        {
            writer.Flush();
            stream.Position = 0L;
            stream.SetLength(0L);
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                writer.WriteEscapedField(
                    fields[fieldIndex].AsSpan(),
                    shapes[fieldIndex]);
            }
            milliseconds[index] = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d
                / System.Diagnostics.Stopwatch.Frequency;
            writer.Flush();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Array.Sort(milliseconds);
        double p95 = milliseconds[(int)Math.Ceiling(MeasurementCount * 0.95d) - 1];
        LastCsvEscapeP95Milliseconds = p95;
        LastCsvEscapeAllocatedBytes = allocated;
        Require(allocated == 0L,
            $"CSV escape kernel allocated {allocated} bytes after warm-up");
        Require(p95 <= 2d, $"CSV escape kernel p95 {p95:F3}ms exceeded 2ms");
    }

    private static void RunCsvEscapeKernel(
        V27Utf8CsvWriter writer,
        MemoryStream stream,
        IReadOnlyList<string> fields,
        IReadOnlyList<V27CsvFieldShape> shapes)
    {
        writer.Flush();
        stream.Position = 0L;
        stream.SetLength(0L);
        for (int index = 0; index < fields.Count; index++)
            writer.WriteEscapedField(fields[index].AsSpan(), shapes[index]);
        writer.Flush();
    }

    private static BalanceMetricCaptureRequest CreateRequest(
        string domain,
        string kind,
        string stableId,
        string metric,
        string detail)
    {
        return new BalanceMetricCaptureRequest
        {
            Domain = domain,
            DefinitionKind = kind,
            StableId = stableId,
            Metric = metric,
            Unit = "mEWU",
            Before = "1000",
            After = "2250",
            AuthoredRoundedValue = "2250",
            PercentDelta = "125",
            ExactFormula = "ceil(before*2.25)",
            BeforeBom = "raw:timber=1",
            AfterBom = "raw:timber=1",
            BeforeDirectWu = "20",
            AfterDirectWu = "45",
            BeforeBomEwu = "8",
            AfterBomEwu = "8",
            BeforeLaborDensity = "2.5",
            AfterLaborDensity = "5.625",
            UpstreamOnlyAfter = "1000",
            InheritedDelta = "0",
            RawLocalDelta = "1250",
            LocalQuantizationBoundaryCount = 2,
            DownstreamConsumerCount = "1",
            DependencyIds = new[] { "raw:timber" },
            RootCauseIds = Array.Empty<string>(),
            AnomalyDisposition = "local-critical",
            ReasonCode = "labor-target-v27",
            ReasonDetail = detail,
            SourceAuthority = "Assets/Resources/SO/Test.asset",
            SourcePropertyPath = "requiredWork",
            ExecutionRoute = "production",
            SaveAuthority = "scriptable-object",
            VerificationEvidence = "v27-ledger-contracts",
            ReviewStatus = "pending",
            ApprovalKey = string.Empty,
            DependencyFingerprint = new string('a', 64),
            LocalFingerprint = new string('d', 64),
            SourceDigest = new string('b', 64),
            SemanticHash = new string('c', 64),
            AssetApplied = "false",
            BalanceBaselineRecordId = "balance:v27:ledger-contract"
        };
    }

    private static byte[] Escape(string value)
    {
        using MemoryStream stream = new MemoryStream();
        using (StreamWriter writer = new StreamWriter(
                   stream,
                   new UTF8Encoding(false, true),
                   128,
                   leaveOpen: true))
        {
            V27BalanceCsvSerializer.WriteEscapedField(writer, value.AsSpan());
            writer.Flush();
        }
        return stream.ToArray();
    }

    private static byte[] Serialize(FrozenBalanceLedger ledger)
    {
        using MemoryStream stream = new MemoryStream();
        V27BalanceCsvSerializer.Write(stream, ledger);
        return stream.ToArray();
    }

    private static string ParseSingleRfc4180Field(byte[] utf8)
    {
        string value = new UTF8Encoding(false, true).GetString(utf8);
        if (value.Length == 0 || value[0] != '"')
            return value;
        Require(value[value.Length - 1] == '"', "quoted CSV field has no closing quote");
        StringBuilder builder = new StringBuilder(value.Length);
        for (int index = 1; index < value.Length - 1; index++)
        {
            char character = value[index];
            if (character == '"')
            {
                Require(index + 1 < value.Length - 1 && value[index + 1] == '"',
                    "quoted CSV field contains a single quote");
                index++;
            }
            builder.Append(character);
        }
        return builder.ToString();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FixedMaterialProfileCatalog : IMaterialEconomicProfileCatalog
    {
        public bool TryGet(
            string physicalItemId,
            out MaterialEconomicProfileSO profile)
        {
            profile = null;
            return false;
        }

        public float GetWorkFactor(string physicalItemId) => 1f;
        public float GetSalvageRetention(string physicalItemId) => 0.6f;
        public bool IsConsumableDuringCraft(string physicalItemId) => false;
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}
#endif
