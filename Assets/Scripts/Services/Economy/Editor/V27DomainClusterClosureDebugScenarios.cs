#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Batch F evidence owner. A row closes only when its live source chain is
/// present and the same editor run executes a normal physical path, a terminal
/// fault/retry, and current-format restore validation.
/// </summary>
[InitializeOnLoad]
public static class V27DomainClusterClosureDebugScenarios
{
    private const string Schema = "v27-domain-cluster-closure.2";
    public const string CsvPath =
        "Artifacts/QA/v27-domain-cluster-closure.csv";
    public const string ReportPath =
        "Artifacts/QA/v27-domain-cluster-closure.txt";
    private const string RequestPath =
        "Temp/v27-domain-cluster-closure.request";
    private const string SelfPath =
        "Assets/Scripts/Services/Economy/Editor/"
        + "V27DomainClusterClosureDebugScenarios.cs";
    private static bool integratedRunQueued;
    private static double integratedRunAfter;

    static V27DomainClusterClosureDebugScenarios()
    {
        EditorApplication.update -= DispatchQueuedIntegratedRun;
        EditorApplication.update += DispatchQueuedIntegratedRun;
    }

    private static readonly ClusterSpec[] Clusters =
    {
        new(
            "agriculture-livestock",
            Link("agriculture.crop-harvest-output",
                "Assets/Scripts/Services/Economy/CropPlotRuntime.cs",
                "class CropPlotRuntime",
                "CropHarvestOutputSaveData",
                "CropHarvestOutputPhase.OutputCommitted"),
            Link("captivity.wildlife-care",
                "Assets/Scripts/Services/Captivity/"
                + "WildlifeCareInputOwnerRuntime.cs",
                "captivity.wildlife-care",
                "ExactGramRequired",
                "TryReconcileRestore"),
            Link("captivity.feed-sink",
                "Assets/Scripts/Services/Captivity/"
                + "CapturedWildlifeFeedOutbox.cs",
                "PhysicalItemDispositionKind.Sink",
                "RecordPending",
                "TryFinalizePending"),
            Link("captivity.feed-terminal-ack",
                "Assets/Scripts/Services/Captivity/"
                + "CapturedWildlifeFeedOutbox.cs",
                "CarePublished",
                "batchDispositions.Acknowledge",
                "ClearPending"),
            Link("captivity.feed-current-format-restore",
                "Assets/Scripts/Models/Captivity/Core/"
                + "CircusSaveValidation.cs",
                "ValidateCapturedWildlifeFeed",
                "pendingFeedCommitId",
                "CapturedWildlifeFeedCommitPhase.CarePublished"),
            Link("captivity.feed-ack-fault",
                "Assets/Scripts/Services/Captivity/Editor/"
                + "CapturedWildlifeFeedOutboxDebugScenarios.cs",
                "FailNextAcknowledgement",
                "JsonUtility.FromJson",
                "TryFinalizePending"),
            RunAgricultureLivestock),
        new(
            "combat-expedition",
            Link("offense.urgent-mitigation-order",
                "Assets/Scripts/Services/Offense/Strategic/"
                + "OffenseUrgentMitigationRuntime.cs",
                "class OffenseUrgentMitigationRuntime",
                "TryFinalizeCompletedOrder",
                "TryReplaceForRestore"),
            Link("offense.urgent-mitigation",
                "Assets/Scripts/Services/Offense/Strategic/"
                + "OffenseUrgentMitigationInputOwnerRuntime.cs",
                "offense.urgent-mitigation",
                "ExactGramRequired",
                "TryReplaceForRestore"),
            Link("offense.urgent-mitigation-transfer",
                "Assets/Scripts/Services/Offense/Strategic/"
                + "OffenseUrgentMitigationRuntime.cs",
                "PhysicalItemDispositionKind.Transfer",
                "ConsumeDeliveredToWip",
                "AcknowledgeWipInput"),
            Link("offense.urgent-mitigation-terminal",
                "Assets/Scripts/Services/Offense/Strategic/"
                + "OffenseUrgentMitigationRuntime.cs",
                "TryRetire",
                "physicalReceiptAcknowledged"),
            Link("offense.world-current-format-restore",
                "Assets/Scripts/Services/Offense/Strategic/"
                + "OffenseWorldStateSaveCodec.cs",
                "BuildRestoreCandidate",
                "PrepareRestore",
                "PublishRestoreCandidate"),
            Link("offense.urgent-terminal-release-fault",
                "Assets/Scripts/Services/Offense/Editor/"
                + "OffenseUrgentMitigationInputOwnerDebugScenarios.cs",
                "releases.Fail = true",
                "TryReplaceForRestore",
                "TryRetire"),
            RunCombatExpedition),
        new(
            "construction-repair-demolition",
            Link("work.construction-order",
                "Assets/Scripts/Services/Character/Work/WorkAmountSystem.cs",
                "class WorkOrderRuntime",
                "TryOpen",
                "TryRequestItem"),
            Link("work.construction",
                "Assets/Scripts/Services/Character/Work/"
                + "WorkConstructionInputOwnerRuntime.cs",
                "work.construction",
                "ExactGramRequired",
                "TryReplaceForRestore"),
            Link("work.material-restitution-transfer",
                "Assets/Scripts/Services/Character/Work/"
                + "WorkOrderMaterialOutbox.cs",
                "PhysicalItemDispositionKind.Transfer",
                "TryPublishRestitution",
                "RestitutionPending"),
            Link("work.construction-terminal-release",
                "Assets/Scripts/Services/Character/Work/WorkAmountSystem.cs",
                "TryPrepareTerminalRelease",
                "TryPublishRestitution"),
            Link("work-order-current-format-restore",
                "Assets/Scripts/Services/Character/Work/"
                + "WorkOrdersSaveSection.cs",
                "ValidateRestitutionOutputCandidate",
                "ValidatePhysicalRestoreCandidate",
                "BuildRestoreCandidate"),
            Link("work.construction-cancel-restore-fault",
                "Assets/Scripts/Services/Character/Work/Editor/"
                + "WorkAmountDebugScenarios.cs",
                "construction cancellation refunds materials",
                "construction restitution output restore preflight",
                "destructive-drain work-order save contract"),
            RunConstructionRepairDemolition),
        new(
            "medical",
            Link("medical.surgical-part-output",
                "Assets/Scripts/Services/Medical/"
                + "SurgicalPartProductionOutputHandler.cs",
                "class SurgicalPartProductionOutputHandler",
                "TryPublish",
                "TryAcknowledge"),
            Link("medical.surgery-material",
                "Assets/Scripts/Services/Medical/"
                + "SurgeryMaterialDestinationRuntime.cs",
                "SurgeryMaterialDestinationAuthority.OwnerDomain",
                "ExactGramRequired",
                "TryReplace"),
            Link("medical.surgery-material-sink",
                "Assets/Scripts/Services/Medical/SurgeryLogisticsRuntime.cs",
                "PhysicalItemDispositionKind.Sink",
                "TryFinalizeConsumedMaterials",
                "TryCommitPending"),
            Link("medical.surgery-material-terminal",
                "Assets/Scripts/Services/Medical/"
                + "SurgeryMaterialTerminalRuntime.cs",
                "class SurgeryMaterialTerminalRuntime",
                "TryBeginOrResume",
                "IsReadyForOwnerClosure"),
            Link("medical.surgery-current-format-restore",
                "Assets/Scripts/Services/Medical/SurgeryRestoreCoordinator.cs",
                "ValidateMaterialTerminalCustodyJoin",
                "TryFinalizePending"),
            Link("medical.surgery-terminal-fault",
                "Assets/Scripts/Services/Medical/Editor/"
                + "SurgeryMaterialTerminalCustodyDebugScenarios.cs",
                "DeferredAdvanceCount = 1",
                "VerifyUpperChildRestoreJoinAndTamperRejection",
                "VerifyBidirectionalOrphanRejection"),
            RunMedical),
        new(
            "research-contracts-rewards",
            Link("economy.regional-contract-runtime",
                "Assets/Scripts/Models/Economy/Content/"
                + "RegionalSupplyContractRuntime.cs",
                "class RegionalSupplyContractRuntime",
                "RecordPending",
                "TryFinalizePending"),
            Link("economy.regional-contract",
                "Assets/Scripts/Services/Economy/"
                + "EconomyProjectInputOwnerRuntime.cs",
                "descriptor.OwnerDomain",
                "ExactGramRequired",
                "TryReplaceForRestore"),
            Link("economy.regional-contract-transfer",
                "Assets/Scripts/Models/Economy/Content/"
                + "RegionalSupplyContractDeliveryOutbox.cs",
                "TryFinalizePending",
                "TryAddContractIncome",
                "AcknowledgeDeliveryTransfer"),
            Link("economy.regional-contract-terminal-ack",
                "Assets/Scripts/Models/Economy/Content/"
                + "RegionalSupplyContractDeliveryOutbox.cs",
                "RewardPublished",
                "Clear(contract)"),
            Link("economy.regional-contract-current-format-restore",
                "Assets/Scripts/Services/Economy/Planning/"
                + "RegionalSupplyContractSaveSection.cs",
                "ValidatePhysicalRestoreCandidate",
                "BuildRestoreCandidate",
                "TryReplaceForRestore"),
            Link("economy.regional-contract-ack-fault",
                "Assets/Scripts/Services/Economy/Editor/"
                + "RegionalSupplyContractTransferOutboxDebugScenarios.cs",
                "FailNextAcknowledgement = true",
                "ValidatePhysicalRestoreCandidate",
                "acknowledgement-only recovery"),
            RunResearchContractsRewards),
        new(
            "shop-economy",
            Link("economy.stock-policy-runtime",
                "Assets/Scripts/Services/Economy/Planning/"
                + "ResourceStockPolicyRuntime.cs",
                "class ResourceStockPolicyRuntime",
                "TryCommitTransferPending",
                "TryFinalizePending"),
            Link("economy.stock-policy",
                "Assets/Scripts/Services/Economy/"
                + "EconomyProjectInputOwnerRuntime.cs",
                "descriptor.OwnerDomain",
                "ExactGramRequired",
                "TryReplaceForRestore"),
            Link("economy.stock-policy-sale-transfer",
                "Assets/Scripts/Models/Economy/Content/"
                + "ResourceStockPolicySaleOutbox.cs",
                "TryFinalizePending",
                "TryPublishSaleIncome",
                "AcknowledgeSaleTransfer"),
            Link("economy.stock-policy-terminal-ack",
                "Assets/Scripts/Services/Economy/Planning/"
                + "ResourceStockPolicyRuntime.cs",
                "TryFinalizePendingSale",
                "PendingSalesByItemId.Remove"),
            Link("economy.stock-policy-current-format-restore",
                "Assets/Scripts/Services/Economy/Planning/"
                + "ResourceStockPolicySaveSection.cs",
                "ValidatePhysicalRestoreCandidate",
                "BuildRestoreCandidate",
                "TryReplaceForRestore"),
            Link("economy.stock-policy-ack-fault",
                "Assets/Scripts/Services/Economy/Editor/"
                + "ResourceStockPolicySaleOutboxDebugScenarios.cs",
                "FailNextAcknowledgement = true",
                "ValidatePhysicalRestoreCandidate",
                "acknowledgement-only completion"),
            RunShopEconomy)
    };

    [MenuItem("DungeonStory/V27/Physical Mass/Run Batch F Domain Clusters")]
    public static void RunIntegratedFromMenu()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        bool dirtyBefore = activeScene.isDirty;
        int[] rootsBefore = CaptureRootInstanceIds(activeScene);
        CaptureResult result = default;
        bool captured = false;
        EditorVerificationSceneFixtureScope.Run(
            "qa:v27-batch-f-domain-clusters",
            () =>
            {
                result = Capture(runFixtures: true);
                captured = true;
            });
        Require(captured,
            "Batch F scratch-scene fixture returned no capture result.");
        Require(SceneManager.GetActiveScene().handle == activeScene.handle
                && activeScene.isDirty == dirtyBefore
                && CaptureRootInstanceIds(activeScene).SequenceEqual(rootsBefore),
            "Batch F integrated fixtures changed the active scene topology or dirty state.");
        Write(result);
        Require(result.Closed == Clusters.Length,
            $"Batch F remains open: {result.Closed}/{Clusters.Length}.");
        Debug.Log("V27_BATCH_F_DOMAIN_CLUSTERS=PASS; closed=6/6");
    }

    private static int[] CaptureRootInstanceIds(Scene scene) => scene
        .GetRootGameObjects()
        .Where(value => value != null)
        .Select(value => value.GetInstanceID())
        .OrderBy(value => value)
        .ToArray();

    /// <summary>
    /// Queues the integrated fixture after an MCP editor command has returned,
    /// so the bridge does not synchronously hold Unity's main-thread progress
    /// modal while the six domain fixtures execute.
    /// </summary>
    public static void QueueIntegratedFromEditorCommand()
    {
        if (integratedRunQueued || File.Exists(RequestPath))
            throw new InvalidOperationException(
                "A Batch F integrated run is already queued.");

        Directory.CreateDirectory("Temp");
        File.WriteAllText(RequestPath, "run", new UTF8Encoding(false));
    }

    private static void DispatchQueuedIntegratedRun()
    {
        if (!integratedRunQueued)
        {
            if (!File.Exists(RequestPath))
                return;

            integratedRunQueued = true;
            integratedRunAfter = EditorApplication.timeSinceStartup + 0.25d;
            return;
        }

        if (EditorApplication.timeSinceStartup < integratedRunAfter)
        {
            return;
        }

        integratedRunQueued = false;
        integratedRunAfter = 0d;
        try
        {
            if (!File.Exists(RequestPath))
                throw new InvalidOperationException(
                    "The queued Batch F request disappeared before dispatch.");
            File.Delete(RequestPath);
            RunIntegratedFromMenu();
        }
        catch (Exception exception)
        {
            Debug.LogError("V27 Batch F queued run failed: " + exception);
        }
    }

    internal static IReadOnlyList<string> CaptureRowIds() =>
        Array.AsReadOnly(Clusters.Select(value => value.Id).ToArray());

    [MenuItem("DungeonStory/V27/Physical Mass/Audit Batch F Domain Clusters")]
    public static void CaptureAuditOnlyFromMenu()
    {
        CaptureResult result = Capture(runFixtures: false);
        Write(result);
        Debug.Log("V27_BATCH_F_DOMAIN_CLUSTERS=AUDIT; structural="
            + result.Structural + "/6; runtime=0/6");
    }

    private static CaptureResult Capture(bool runFixtures)
    {
        ValidateRegistry();
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root unavailable.");
        ClusterResult[] rows = Clusters.Select(cluster =>
            Evaluate(root, cluster, runFixtures)).ToArray();
        string sourceDigest = ComputeSourceDigest(root);
        byte[] csv = BuildCsv(rows, sourceDigest);
        byte[] report = BuildReport(rows, sourceDigest);
        Require(csv.SequenceEqual(BuildCsv(rows, sourceDigest))
                && report.SequenceEqual(BuildReport(rows, sourceDigest)),
            "Batch F serialization is not byte deterministic.");
        return new CaptureResult(csv, report,
            rows.Count(value => value.Structural),
            rows.Count(value => value.Closed));
    }

    private static ClusterResult Evaluate(
        string root,
        ClusterSpec cluster,
        bool runFixture)
    {
        string structuralFailure = string.Empty;
        foreach (EvidenceLink link in cluster.Links)
        {
            string absolute = Path.Combine(root,
                link.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolute))
            {
                structuralFailure = "missing-source:" + link.Id;
                break;
            }
            string source = File.ReadAllText(absolute);
            string missing = link.RequiredFragments.FirstOrDefault(fragment =>
                source.IndexOf(fragment, StringComparison.Ordinal) < 0);
            if (missing != null)
            {
                structuralFailure = "missing-symbol:" + link.Id;
                break;
            }
        }

        bool structural = structuralFailure.Length == 0;
        if (!runFixture || !structural)
        {
            return new ClusterResult(cluster, structural, false,
                structural ? "OPEN:unity-execution-required"
                    : "OPEN:" + structuralFailure);
        }

        try
        {
            cluster.RunFixture();
            return new ClusterResult(cluster, true, true,
                "PASS:current-source-integrated-fixture");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return new ClusterResult(cluster, true, false,
                "OPEN:fixture-failed:" + exception.GetType().Name);
        }
    }

    private static void RunAgricultureLivestock()
    {
        CropPlotInputOwnerDebugScenarios.RunAll();
        WildlifeCareInputOwnerDebugScenarios.RunAll();
        Require(CapturedWildlifeFeedOutboxDebugScenarios.Run().Length > 0,
            "Captive feed fixture returned no evidence.");
    }

    private static void RunCombatExpedition()
    {
        OffenseUrgentMitigationInputOwnerDebugScenarios.RunAll();
        Require(OffenseStrategicDebugScenarios.RunAll().Contains(
                "passed", StringComparison.OrdinalIgnoreCase),
            "Offense strategic fixture returned no PASS evidence.");
    }

    private static void RunConstructionRepairDemolition()
    {
        InvokeStaticVoid("WorkConstructionInputOwnerDebugScenarios", "Run");
        Require(WorkAmountDebugScenarios.RunAll(logSuccess: false),
            "Work amount cluster fixture failed.");
    }

    private static void RunMedical()
    {
        InvokeStaticVoid(
            "SurgicalPartStorageInputOwnerDebugScenarios", "RunAll");
        Require(SurgeryMaterialDestinationRuntimeDebugScenarios.RunAll(
                log: false),
            "Surgery material destination fixture failed.");
        SurgeryMaterialTerminalCustodyDebugScenarios.RunAll();
        SurgicalPartPreparedOutputDebugScenarios.RunAll();
        SurgicalPartProductionOutputCrossAggregateSaveValidationDebugScenarios
            .RunAll();
    }

    private static void RunResearchContractsRewards()
    {
        InvokeStaticVoid("EconomyProjectInputOwnerDebugScenarios", "RunAll");
        Require(RegionalSupplyContractTransferOutboxDebugScenarios.Verify()
                .Contains("PASS", StringComparison.Ordinal),
            "Regional contract fixture returned no PASS evidence.");
    }

    private static void RunShopEconomy()
    {
        InvokeStaticVoid("EconomyProjectInputOwnerDebugScenarios", "RunAll");
        Require(ResourceStockPolicySaleOutboxDebugScenarios.Verify()
                .Contains("PASS", StringComparison.Ordinal),
            "Stock-policy sale fixture returned no PASS evidence.");
        Require(FacilityShopDebugScenarios.RunAll(logSuccess: false),
            "Facility-shop fixture failed.");
    }

    private static void ValidateRegistry()
    {
        Require(Clusters.Length == 6,
            "Batch F denominator drifted from six clusters.");
        string[] ids = Clusters.Select(value => value.Id).ToArray();
        Require(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length
                && ids.SequenceEqual(ids.OrderBy(value => value,
                    StringComparer.Ordinal), StringComparer.Ordinal),
            "Batch F cluster IDs must be unique and ordinal sorted.");
        Require(Clusters.All(value => value.Links.Count == 6
                && value.Links.Select(link => link.Id)
                    .Distinct(StringComparer.Ordinal).Count() == 6),
            "Every Batch F row must own six distinct evidence links.");
    }

    private static byte[] BuildCsv(
        IReadOnlyList<ClusterResult> rows,
        string sourceDigest)
    {
        using MemoryStream stream = new();
        V27Utf8CsvWriter writer = new(stream, 8192);
        WriteRow(writer, new[]
        {
            "schemaVersion", "batch", "rowId", "producerOwnerId",
            "producerEvidence", "exactInputOwnerId", "exactInputEvidence",
            "consumerDispositionId", "consumerEvidence", "terminalDrainId",
            "terminalEvidence", "currentFormatRestoreId", "restoreEvidence",
            "faultId", "faultEvidence", "structuralStatus", "runtimeStatus",
            "reviewStatus", "evidenceToken", "sourceDigest"
        });
        foreach (ClusterResult row in rows)
        {
            IReadOnlyList<EvidenceLink> links = row.Spec.Links;
            WriteRow(writer, new[]
            {
                Schema, "F", row.Spec.Id,
                links[0].Id, links[0].Evidence,
                links[1].Id, links[1].Evidence,
                links[2].Id, links[2].Evidence,
                links[3].Id, links[3].Evidence,
                links[4].Id, links[4].Evidence,
                links[5].Id, links[5].Evidence,
                row.Structural ? "PASS" : "OPEN",
                row.Closed ? "PASS" : "OPEN",
                row.Status,
                row.Closed ? row.Spec.Id + ":" + sourceDigest : string.Empty,
                sourceDigest
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] BuildReport(
        IReadOnlyList<ClusterResult> rows,
        string sourceDigest)
    {
        int structural = rows.Count(value => value.Structural);
        int closed = rows.Count(value => value.Closed);
        StringBuilder report = new();
        report.Append(closed == 6 ? "RESULT=PASS" : "RESULT=IN_PROGRESS")
            .Append("; batch=F; structural=").Append(structural)
            .Append("/6; closed=").Append(closed)
            .Append("/6; open=").Append(6 - closed).Append('\n')
            .Append("authority=live-source-six-link-registry+integrated-fixture\n")
            .Append("requiredEvidence=producer>exact-input-owner>"
                + "typed-consumer>terminal-drain>current-format-restore>fault\n");
        foreach (ClusterResult row in rows)
        {
            report.Append("cluster=").Append(row.Spec.Id)
                .Append("; structural=")
                .Append(row.Structural ? "PASS" : "OPEN")
                .Append("; runtime=")
                .Append(row.Closed ? "PASS" : "OPEN")
                .Append("; status=").Append(row.Status).Append('\n');
        }
        report.Append("sourceDigest=").Append(sourceDigest).Append('\n')
            .Append("currentSourceDigest=")
            .Append(V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest())
            .Append('\n')
            .Append("gameplaySceneSha256=")
            .Append(V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest())
            .Append('\n');
        return new UTF8Encoding(false, true).GetBytes(report.ToString());
    }

    private static string ComputeSourceDigest(string root)
    {
        string[] paths = Clusters.SelectMany(value => value.Links)
            .Select(value => value.Path)
            .Append(SelfPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        using SHA256 sha = SHA256.Create();
        foreach (string path in paths)
        {
            string absolute = Path.Combine(root,
                path.Replace('/', Path.DirectorySeparatorChar));
            string normalized = File.Exists(absolute)
                ? File.ReadAllText(absolute).Replace("\r\n", "\n")
                : "<MISSING>";
            byte[] bytes = Encoding.UTF8.GetBytes(path + "\n" + normalized + "\n");
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Hex(sha.Hash);
    }

    private static void Write(CaptureResult result)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(
            CsvPath, stream => stream.Write(result.Csv, 0, result.Csv.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(
            ReportPath,
            stream => stream.Write(result.Report, 0, result.Report.Length));
    }

    private static void WriteRow(
        V27Utf8CsvWriter writer,
        IReadOnlyList<string> fields)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            if (index > 0)
                writer.WriteAscii(',');
            writer.WriteEscapedField((fields[index] ?? string.Empty).AsSpan());
        }
        writer.WriteCrLf();
    }

    private static void InvokeStaticVoid(string typeName, string methodName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName, throwOnError: false))
            .FirstOrDefault(value => value != null)
            ?? throw new InvalidOperationException(
                "Batch F fixture type is unavailable: " + typeName);
        System.Reflection.MethodInfo method = type.GetMethod(
            methodName,
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null)
            ?? throw new InvalidOperationException(
                "Batch F fixture method is unavailable: "
                + typeName + "." + methodName);
        method.Invoke(null, null);
    }

    private static EvidenceLink Link(
        string id,
        string path,
        params string[] fragments) => new(id, path, fragments);

    private static string Hex(byte[] bytes)
    {
        const string alphabet = "0123456789abcdef";
        char[] result = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            result[index * 2] = alphabet[bytes[index] >> 4];
            result[index * 2 + 1] = alphabet[bytes[index] & 0x0f];
        }
        return new string(result);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class EvidenceLink
    {
        public EvidenceLink(string id, string path, IReadOnlyList<string> fragments)
        {
            Id = id;
            Path = path;
            RequiredFragments = fragments ?? Array.Empty<string>();
        }

        public string Id { get; }
        public string Path { get; }
        public IReadOnlyList<string> RequiredFragments { get; }
        public string Evidence => Path + "#" + Id;
    }

    private sealed class ClusterSpec
    {
        public ClusterSpec(
            string id,
            EvidenceLink producer,
            EvidenceLink input,
            EvidenceLink consumer,
            EvidenceLink terminal,
            EvidenceLink restore,
            EvidenceLink fault,
            Action runFixture)
        {
            Id = id;
            Links = new[] { producer, input, consumer, terminal, restore, fault };
            RunFixture = runFixture;
        }

        public string Id { get; }
        public IReadOnlyList<EvidenceLink> Links { get; }
        public Action RunFixture { get; }
    }

    private readonly struct ClusterResult
    {
        public ClusterResult(
            ClusterSpec spec,
            bool structural,
            bool closed,
            string status)
        {
            Spec = spec;
            Structural = structural;
            Closed = closed;
            Status = status;
        }

        public ClusterSpec Spec { get; }
        public bool Structural { get; }
        public bool Closed { get; }
        public string Status { get; }
    }

    private readonly struct CaptureResult
    {
        public CaptureResult(byte[] csv, byte[] report, int structural, int closed)
        {
            Csv = csv;
            Report = report;
            Structural = structural;
            Closed = closed;
        }

        public byte[] Csv { get; }
        public byte[] Report { get; }
        public int Structural { get; }
        public int Closed { get; }
    }
}
#endif
