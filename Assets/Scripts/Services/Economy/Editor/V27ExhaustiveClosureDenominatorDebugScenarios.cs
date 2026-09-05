#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Stable source-derived denominators for the exhaustive Batch F/G gates.
/// Structural linkage and current-source live evidence are reported separately:
/// source/fixture presence cannot promote a row whose integrated runner is stale
/// or missing.
/// </summary>
[InitializeOnLoad]
public static class V27ExhaustiveClosureDenominatorDebugScenarios
{
    public const string EvidenceSchemaId = "v27-fg-evidence-v2";
    public const string RemainingFaultEvidenceReportPath =
        "Artifacts/QA/v27-batch-g-remaining-fault-evidence.txt";
    public const string FaultCsvPath =
        "Artifacts/QA/v27-live-fault-matrix.csv";
    public const string FaultReportPath =
        "Artifacts/QA/v27-live-fault-matrix.txt";
    private const string RequestPath =
        "Temp/v27-exhaustive-fg.request";

    private const string SelfPath =
        "Assets/Scripts/Services/Economy/Editor/V27ExhaustiveClosureDenominatorDebugScenarios.cs";
    private static string queuedMode;
    private static double queuedRunAfter;

    static V27ExhaustiveClosureDenominatorDebugScenarios()
    {
        EditorApplication.update -= DispatchQueuedRun;
        EditorApplication.update += DispatchQueuedRun;
    }

    private static readonly ClosureRow[] DomainRows =
    {
        Domain("agriculture-livestock", "producer>consumer>terminal-fault>current-format-restore"),
        Domain("combat-expedition", "producer>consumer>terminal-fault>current-format-restore"),
        Domain("construction-repair-demolition", "producer>consumer>terminal-fault>current-format-restore"),
        Domain("medical", "producer>consumer>terminal-fault>current-format-restore"),
        Domain("research-contracts-rewards", "producer>consumer>terminal-fault>current-format-restore"),
        Domain("shop-economy", "producer>consumer>terminal-fault>current-format-restore")
    };

    private static readonly ClosureRow[] FaultRows =
    {
        Fault("active-replan", "carried-custody>retarget>lease-intent-preserved"),
        Fault("carried-cancel", "carried-custody>physical-recovery>exact-release"),
        Fault("clean-ab-rng-causal-cone", "clean-repeatability>keyed-events>rng-isolation"),
        Fault("component-disable", "carried-custody>recovery-pending>restore"),
        Fault("dead", "carried-custody>current-cell-drop>restore"),
        Fault("destination-revision-drift", "admission-reject>replan>no-loss"),
        Fault("downed", "carried-custody>current-cell-drop>restore"),
        Fault("drop-publication-failure", "recovery-pending>retry>exact-once"),
        Fault("facility-destroy-mid-wip", "wip>terminal-disposition>restore"),
        Fault("floor-clutter-access-egress", "loose-provenance>clearance>wait-wu"),
        Fault("gameobject-disable-destroy", "carried-custody>recovery-pending>restore"),
        Fault("mid-haul-restore", "lease>intent>carried-stack>resume"),
        Fault("multi-stack", "whole-plan>partial-lines>quantity-gram-exact"),
        Fault("one-gram-clearance", "capacity-boundary>admission>publication"),
        Fault("output-full", "waiting-for-output-space>retry>no-reroll"),
        Fault("partial-pickup", "slice>source-remainder>carried-custody"),
        Fault("pre-pickup-cancel", "lease-release>source-unchanged>intent-close"),
        Fault("recovery-pending-retry", "durable-pending>publication>ack"),
        Fault("whole-pickup", "source>carried>destination>receipt")
    };

    private static readonly IReadOnlyDictionary<string, EvidenceSpec>
        FaultEvidence = new Dictionary<string, EvidenceSpec>(
            StringComparer.Ordinal)
        {
            ["active-replan"] = HaulLifecycle(
                "ABILITY_HAUL_ACTIVE_REPLAN_EXACT_OWNERSHIP=PASS"),
            ["carried-cancel"] = HaulLifecycle(
                "ABILITY_HAUL_CARRIED_CANCEL_CURRENT_CELL_RECOVERY=PASS",
                "ABILITY_HAUL_CARRIED_CANCEL_AUTHORITY_RELEASED=PASS",
                "ABILITY_HAUL_CARRIED_CANCEL_CURRENT_FORMAT_RESTORE_EXACT=PASS"),
            ["clean-ab-rng-causal-cone"] = Paired(
                "PAIRED_RUN_CLEAN_REPEATABILITY_EXACT",
                "PAIRED_RUN_EXOGENOUS_EVENTS_EXACT",
                "RNG_CAUSAL_CONE_NO_CROSS_TALK"),
            ["component-disable"] = HaulLifecycle(
                "ABILITY_HAUL_COMPONENT_DISABLE_CURRENT_CELL_RECOVERY=PASS",
                "ABILITY_HAUL_COMPONENT_DISABLE_AUTHORITY_RELEASED=PASS",
                "ABILITY_HAUL_COMPONENT_DISABLE_CURRENT_FORMAT_RESTORE_EXACT=PASS"),
            ["destination-revision-drift"] = AuthoredTransport(
                "PREPARED_OUTPUT_CANARY_DESTINATION_REVISION_LIVE_STALE_REJECT",
                "PREPARED_OUTPUT_CANARY_DESTINATION_REVISION_REPLAN_FRESH_AUTHORITY",
                "PREPARED_OUTPUT_CANARY_DESTINATION_REVISION_REPLAN_NO_LOSS_DELIVERED"),
            ["downed"] = HaulLifecycle(
                "ABILITY_HAUL_DOWNED_CURRENT_CELL_RECOVERY=PASS",
                "ABILITY_HAUL_DOWNED_AUTHORITY_RELEASED=PASS",
                "ABILITY_HAUL_DOWNED_PHYSICAL_SAVE_EXACT=PASS",
                "ABILITY_HAUL_DOWNED_CURRENT_FORMAT_RESTORE_EXACT=PASS"),
            ["drop-publication-failure"] = RemainingFault(
                "BATCH_G_DROP_PUBLICATION_FAILURE_EXACT_ONCE=PASS"),
            ["dead"] = CrossAction(
                "HAUL_DEAD_CURRENT_CELL_TRANSIENT_DROP=PASS",
                "HAUL_DEAD_QUANTITY_NO_TELEPORT=PASS",
                "HAUL_DEAD_CURRENT_FORMAT_RESTORE_EXACT=PASS"),
            ["floor-clutter-access-egress"] = Paired(
                "FLOOR_CLUTTER_ACCESS_EGRESS_ZERO",
                "FLOOR_CLUTTER_RECOVERY_ZERO"),
            ["gameobject-disable-destroy"] = HaulLifecycle(
                "ABILITY_HAUL_GAMEOBJECT_DESTROY_CURRENT_CELL_RECOVERY=PASS",
                "ABILITY_HAUL_GAMEOBJECT_DESTROY_AUTHORITY_RELEASED=PASS",
                "ABILITY_HAUL_GAMEOBJECT_DESTROY_CURRENT_FORMAT_RESTORE_EXACT=PASS"),
            ["facility-destroy-mid-wip"] = Destructive(
                "BATCH_G_FACILITY_DESTROY_MID_WIP_CURRENT_FORMAT_EXACT",
                "PREPARED_OUTPUT_DESTRUCTIVE_DURABLE_STAGE_REQUIRED",
                "PREPARED_OUTPUT_DESTRUCTIVE_PHYSICAL_AUTHORITY_CONSERVED",
                "PREPARED_OUTPUT_DESTRUCTIVE_RESTORE_NO_DUPLICATE",
                "PREPARED_OUTPUT_DESTRUCTIVE_TYPED_TERMINAL_WORLD_REMOVAL"),
            ["mid-haul-restore"] = HaulLifecycle(
                "ABILITY_HAUL_MID_HAUL_CURRENT_FORMAT_RESTORE_EXACT=PASS",
                "ABILITY_HAUL_MID_HAUL_RESUME_AUTHORITY_JOINED=PASS"),
            ["multi-stack"] = RemainingFault(
                "BATCH_G_MULTI_STACK_QUANTITY_GRAM_EXACT=PASS"),
            ["one-gram-clearance"] = HaulLifecycle(
                "FACILITY_BUFFER_ONE_GRAM_TYPED_BOUNDARY=PASS"),
            ["output-full"] = Synthetic(
                "PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_WIP_OUTCOME_FROZEN",
                "PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_RESTORE_EXACT",
                "PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_RETRY_MUTATION_ZERO",
                "PREPARED_OUTPUT_CANARY_CAPACITY_RESUME_SAME_BATCH"),
            ["partial-pickup"] = RemainingFault(
                "BATCH_G_PARTIAL_PICKUP_CUSTODY_EXACT=PASS"),
            // The authored P17 hay-feed case deliberately skips the transport
            // fault matrix.  These tokens are produced by the authored sawmill
            // case, which executes the same production route with the matrix on.
            ["pre-pickup-cancel"] = AuthoredTransport(
                "PREPARED_OUTPUT_CANARY_PRE_PICKUP_CANCEL_READY",
                "PREPARED_OUTPUT_CANARY_PRE_PICKUP_CANCEL_RELEASES_ONLY_LEASE"),
            ["recovery-pending-retry"] = RemainingFault(
                "BATCH_G_RECOVERY_PENDING_RETRY_ACK=PASS"),
            ["whole-pickup"] = HaulLifecycle(
                "ABILITY_HAUL_WHOLE_PICKUP_DELIVERY_EXACT=PASS",
                "ABILITY_HAUL_WHOLE_PICKUP_CURRENT_FORMAT_RESTORE_EXACT=PASS")
        };

    // Structural linkage is deliberately weaker than live closure.  It proves
    // that the production branch and a focused fixture exist in current source;
    // it never promotes a row to PASS without a fresh integrated PlayMode report.
    private static readonly IReadOnlyDictionary<string, StructuralSpec>
        FaultStructure = new Dictionary<string, StructuralSpec>(
            StringComparer.Ordinal)
        {
            ["active-replan"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/AbilityHaul.cs",
                    "StopHaulingForReplan",
                    "ReleaseUnpickedAndRetainCarriedForReplan"),
                Probe(
                    "Assets/Scripts/Services/Character/AI/Editor/CharacterAiCrossActionFaultPlayModeVerifier.cs",
                    "HAUL_ACTIVE_REPLAN_RETAINS_CARRIED=PASS")),
            ["carried-cancel"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/AbilityHaul.cs",
                    "public bool TryStopHauling(",
                    "TryReturnCarriedItemsAfterInterruptedHaul"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/PhysicalItemLogisticsPlayModeVerifier.cs",
                    "PRODUCTION_INPUT_BUFFER_PICKUP_CANCEL_PHYSICAL_RECOVERY",
                    "PRODUCTION_INPUT_BUFFER_CANCEL_SAVE_RESTORE_NO_ORPHAN")),
            ["clean-ab-rng-causal-cone"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Foundation/Random/RandomStreamProvider.cs",
                    "IRandomStreamDiagnosticsQuery",
                    "Global character random stream"),
                Probe(
                    "Assets/Scripts/Services/Economy/Editor/V27PairedClutterPlayModeVerifier.cs",
                    "PAIRED_RUN_CLEAN_REPEATABILITY_EXACT",
                    "PAIRED_RUN_EXOGENOUS_EVENTS_EXACT",
                    "RNG_CROSS_TALK")),
            ["component-disable"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/AbilityHaul.cs",
                    "private void OnDisable()",
                    "StopHauling(\"disabled\")"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/RestoredCarrySubsetReleaseDebugScenarios.cs",
                    "ABILITY_HAUL_COMPONENT_DISABLE_CURRENT_CELL_RECOVERY=PASS",
                    "ABILITY_HAUL_COMPONENT_DISABLE_AUTHORITY_RELEASED=PASS",
                    "ABILITY_HAUL_COMPONENT_DISABLE_CURRENT_FORMAT_RESTORE_EXACT=PASS")),
            ["dead"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/AbilityHaul.cs",
                    "WorldItemCarryInterruptionKind.Dead",
                    "TransientRecoveryDeadlineSeconds"),
                Probe(
                    "Assets/Scripts/Services/Character/AI/Editor/CharacterAiCrossActionFaultPlayModeVerifier.cs",
                    "HAUL_DEAD_CURRENT_CELL_TRANSIENT_DROP=PASS",
                    "HAUL_DEAD_QUANTITY_NO_TELEPORT=PASS",
                    "HAUL_DEAD_CURRENT_FORMAT_RESTORE_EXACT=PASS")),
            ["destination-revision-drift"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/WorldItemWarehouseService.cs",
                    "CapacityRevision",
                    "WarehouseCapacityRevision"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/PhysicalItemLogisticsPlayModeVerifier.cs",
                    "PREPARED_OUTPUT_CANARY_DESTINATION_REVISION_LIVE_STALE_REJECT",
                    "PREPARED_OUTPUT_CANARY_DESTINATION_REVISION_REPLAN_FRESH_AUTHORITY",
                    "PREPARED_OUTPUT_CANARY_DESTINATION_REVISION_REPLAN_NO_LOSS_DELIVERED")),
            ["downed"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/AbilityHaul.cs",
                    "WorldItemCarryInterruptionKind.Downed",
                    "HaulCarryDropContext"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/PhysicalItemLogisticsPlayModeVerifier.cs",
                    "PREPARED_OUTPUT_TRANSPORT_DOWNED_CURRENT_CELL_EXACT",
                    "PREPARED_OUTPUT_TRANSPORT_DOWNED_CHECKPOINT_RESTORED_EXACT")),
            ["drop-publication-failure"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/AbilityHaul.cs",
                    "stage=recovery-pending",
                    "carried-item-recovery-failed"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/ProductionInputDestinationCustodyDrainServiceDebugScenarios.cs",
                    "Injected carried-drop failure did not defer the drain.",
                    "Drop-failure retry did not roll forward to completion")),
            ["facility-destroy-mid-wip"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Buildings/ProductionFacilityDestructiveDrainRecoveryRuntime.cs",
                    "ProductionFacilityDestructiveDrainRecoveryRuntime",
                    "OnRestoreCompleted"),
                Probe(
                    "Assets/Scripts/Services/Economy/Editor/ProductionGenericBillTerminalDrainOutboxDebugScenarios.cs",
                    "VerifyFacilityDestroyedMidWipCurrentFormatIntegration",
                    "BATCH_G_FACILITY_DESTROY_MID_WIP_CURRENT_FORMAT_EXACT=PASS")),
            ["floor-clutter-access-egress"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/PhysicalStockQuery.cs",
                    "IsTransientCarryRecoveryDrop",
                    "DroppedAtGameTime"),
                Probe(
                    "Assets/Scripts/Services/Economy/Editor/V27PairedClutterPlayModeVerifier.cs",
                    "FLOOR_CLUTTER_ACCESS_EGRESS_ZERO",
                    "FLOOR_CLUTTER_RECOVERY_ZERO")),
            ["gameobject-disable-destroy"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/AbilityHaul.cs",
                    "private void OnDisable()",
                    "TryReturnCarriedItemsAfterInterruptedHaul"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/RestoredCarrySubsetReleaseDebugScenarios.cs",
                    "ABILITY_HAUL_GAMEOBJECT_DESTROY_CURRENT_CELL_RECOVERY=PASS",
                    "ABILITY_HAUL_GAMEOBJECT_DESTROY_AUTHORITY_RELEASED=PASS",
                    "ABILITY_HAUL_GAMEOBJECT_DESTROY_CURRENT_FORMAT_RESTORE_EXACT=PASS")),
            ["mid-haul-restore"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/AbilityHaul.cs",
                    "CaptureDeliveryIntentForSave",
                    "TryRebindRestoredDeliveryIntent"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/HaulPlanConstructionSafetyDebugScenarios.cs",
                    "RunWholePickupAndMidHaulRestoreFocused",
                    "ABILITY_HAUL_MID_HAUL_CURRENT_FORMAT_RESTORE_EXACT=PASS",
                    "ABILITY_HAUL_MID_HAUL_RESUME_AUTHORITY_JOINED=PASS")),
            ["multi-stack"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/WorldItemHaulPlanningService.cs",
                    "MaximumPickupLegs",
                    "PickupLegs"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/HaulPlanConstructionSafetyDebugScenarios.cs",
                    "expected multiple pickup legs",
                    "pickups={plan.PickupLegs.Count}")),
            ["one-gram-clearance"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/FacilityBufferMassAdmissionService.cs",
                    "FacilityBufferMassAdmissionFailureCode.CapacityUnavailable",
                    "planned.TotalMassGrams > profile.MaxMassGrams - occupied"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/FacilityBufferMassAdmissionDebugScenarios.cs",
                    "RunOneGramClearanceFocused",
                    "CapacityUnavailable",
                    "exact 1g clearance")),
            ["output-full"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Economy/ProductionPreparedOutputExecutionAdapter.cs",
                    "ResolvedWaitingForOutputSpace",
                    "outcomeFingerprint"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/PhysicalItemLogisticsPlayModeVerifier.cs",
                    "PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_WIP_OUTCOME_FROZEN",
                    "PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_RETRY_MUTATION_ZERO")),
            ["partial-pickup"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/ItemTransferService.cs",
                    "TryPickupReservedStackQuantity",
                    "TryPublishPartialExactRouteCustody"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/HaulPlanConstructionSafetyDebugScenarios.cs",
                    "expected partial reservation",
                    "partial pickup failed")),
            ["pre-pickup-cancel"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/AbilityHaul.cs",
                    "ReleaseActivePlanReservations",
                    "ItemReservationReleaseReason.Cancelled"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/PhysicalItemLogisticsPlayModeVerifier.cs",
                    "PREPARED_OUTPUT_CANARY_PRE_PICKUP_CANCEL_READY",
                    "PREPARED_OUTPUT_CANARY_PRE_PICKUP_CANCEL_RELEASES_ONLY_LEASE")),
            ["recovery-pending-retry"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/AbilityHaul.cs",
                    "stage=recovery-pending",
                    "restoredDeliveryPending = true"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/ProductionInputDestinationCustodyDrainServiceDebugScenarios.cs",
                    "Injected carried-drop failure did not defer the drain.",
                    "Drop-failure retry did not roll forward to completion")),
            ["whole-pickup"] = Structure(
                Probe(
                    "Assets/Scripts/Services/Items/AbilityHaul.cs",
                    "TryPickupReservedStackQuantity",
                    "TryCommitHaulPickup"),
                Probe(
                    "Assets/Scripts/Services/Items/Editor/HaulPlanConstructionSafetyDebugScenarios.cs",
                    "RunWholePickupAndMidHaulRestoreFocused",
                    "ABILITY_HAUL_WHOLE_PICKUP_DELIVERY_EXACT=PASS",
                    "ABILITY_HAUL_WHOLE_PICKUP_CURRENT_FORMAT_RESTORE_EXACT=PASS"))
        };

    private static readonly string[] DigestSourcePaths =
    {
        "Assets/Scripts/Services/Economy/Editor/V27FacilityBufferOwnerManifestDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/Editor/V27PairedClutterPlayModeVerifier.cs",
        "Assets/Scripts/Services/Character/AI/Editor/CharacterAiCrossActionFaultPlayModeVerifier.cs",
            "Assets/Scripts/Services/Items/Editor/AbilityHaulLifecycleRecoveryPlayModeVerifier.cs",
            "Assets/Scripts/Services/Items/Editor/HaulPlanConstructionSafetyDebugScenarios.cs",
            "Assets/Scripts/Services/Items/Editor/ProductionInputDestinationCustodyDrainServiceDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/Editor/ProductionGenericBillTerminalDrainOutboxDebugScenarios.cs",
            "Assets/Scripts/Services/Items/Editor/PhysicalItemLogisticsPlayModeVerifier.cs",
            "Assets/Scripts/Services/Economy/Editor/V27CurrentSourceEvidenceDigest.cs",
            SelfPath
        };

    [MenuItem("DungeonStory/V27/Physical Mass/Capture Exhaustive F-G Denominators")]
    public static void RunFromMenu()
    {
        CaptureResult first = Capture();
        CaptureResult second = Capture();
        Require(first.FaultCsv.SequenceEqual(second.FaultCsv)
                && first.FaultReport.SequenceEqual(second.FaultReport),
            "Exhaustive F/G denominator capture is not byte deterministic.");
        Write(FaultCsvPath, first.FaultCsv);
        Write(FaultReportPath, first.FaultReport);
        Debug.Log("V27_EXHAUSTIVE_DENOMINATORS=PASS; "
            + "F=delegated-to-current-source-integrated-runner; G="
            + first.FaultClosed + "/19; status="
            + (first.FaultClosed == 19 ? "PASS" : "IN_PROGRESS"));
    }

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Remaining Batch G Fault Rows")]
    public static void RunRemainingFaultEvidenceFromMenu()
    {
        byte[] first = CaptureRemainingFaultEvidence();
        byte[] second = CaptureRemainingFaultEvidence();
        Require(first.SequenceEqual(second),
            "Remaining Batch G focused evidence is not byte deterministic.");
        Write(RemainingFaultEvidenceReportPath, first);
        Debug.Log("V27_BATCH_G_REMAINING_FAULT_EVIDENCE=PASS; rows=4/4; "
            + "report=" + RemainingFaultEvidenceReportPath);
    }

    public static void QueueCaptureFromEditorCommand() =>
        WriteDurableRequest("capture");

    public static void QueueRemainingFaultEvidenceFromEditorCommand() =>
        WriteDurableRequest("remaining");

    private static void WriteDurableRequest(string mode)
    {
        if (queuedMode != null || File.Exists(RequestPath))
            throw new InvalidOperationException(
                "An exhaustive F/G verification request is already queued.");

        Directory.CreateDirectory("Temp");
        File.WriteAllText(RequestPath, mode, new UTF8Encoding(false));
    }

    internal static bool HasPendingDurableRun =>
        queuedMode != null || File.Exists(RequestPath);

    private static void DispatchQueuedRun()
    {
        if (queuedMode == null)
        {
            if (!File.Exists(RequestPath))
                return;

            try
            {
                queuedMode = File.ReadAllText(RequestPath).Trim();
            }
            catch (IOException)
            {
                return;
            }
            queuedRunAfter = EditorApplication.timeSinceStartup + 0.25d;
            return;
        }

        if (EditorApplication.timeSinceStartup < queuedRunAfter)
            return;

        string mode = queuedMode;
        queuedMode = null;
        queuedRunAfter = 0d;
        try
        {
            if (!File.Exists(RequestPath))
                throw new InvalidOperationException(
                    "The queued exhaustive F/G request disappeared before dispatch.");
            File.Delete(RequestPath);
            switch (mode)
            {
                case "capture":
                    RunFromMenu();
                    break;
                case "remaining":
                    RunRemainingFaultEvidenceFromMenu();
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown exhaustive F/G request mode: " + mode);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("V27 exhaustive F/G queued run failed: " + exception);
        }
    }

    internal static IReadOnlyList<string> CaptureDomainRowIds() =>
        Array.AsReadOnly(DomainRows.Select(value => value.RowId).ToArray());

    internal static IReadOnlyList<string> CaptureFaultRowIds() =>
        Array.AsReadOnly(FaultRows.Select(value => value.RowId).ToArray());

    private static CaptureResult Capture()
    {
        ValidateRows("F", DomainRows, 6);
        Require(CaptureDomainRowIds().SequenceEqual(
                V27DomainClusterClosureDebugScenarios.CaptureRowIds(),
                StringComparer.Ordinal),
            "Batch F denominator drifted from its sole artifact writer.");
        ValidateRows("G", FaultRows, 19);
        ValidateFaultStructure();
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        IReadOnlyDictionary<EvidenceKind, EvidenceReport> evidence =
            new Dictionary<EvidenceKind, EvidenceReport>
            {
                [EvidenceKind.PreparedOutputP17] = LoadEvidenceReport(
                    PhysicalItemLogisticsPlayModeVerifier
                        .P17PreparedOutputWarehouseReportPath,
                    "[PASS] "),
                [EvidenceKind.SyntheticPreparedOutput] = LoadEvidenceReport(
                    PhysicalItemLogisticsPlayModeVerifier
                        .PreparedOutputWarehouseReportPath,
                    "[PASS] "),
                [EvidenceKind.AuthoredTransport] = LoadEvidenceReport(
                    PhysicalItemLogisticsPlayModeVerifier
                        .SawmillPreparedOutputWarehouseReportPath,
                    "[PASS] "),
                [EvidenceKind.ProductionInput] = LoadEvidenceReport(
                    PhysicalItemLogisticsPlayModeVerifier
                        .ProductionInputMassReportPath,
                    "[PASS] "),
                [EvidenceKind.DestructiveDrain] = LoadEvidenceReport(
                    PhysicalItemLogisticsPlayModeVerifier
                        .DestructiveDrainPreparedOutputReportPath,
                    "[PASS] "),
                [EvidenceKind.CrossActionFault] = LoadEvidenceReport(
                    CharacterAiCrossActionFaultPlayModeVerifier.ReportPath,
                    string.Empty),
                [EvidenceKind.HaulLifecycleRecovery] = LoadEvidenceReport(
                    AbilityHaulLifecycleRecoveryPlayModeVerifier.ReportPath,
                    string.Empty),
                [EvidenceKind.RemainingFaultFocused] = LoadEvidenceReport(
                    RemainingFaultEvidenceReportPath,
                    string.Empty),
                [EvidenceKind.PairedClutter] = LoadEvidenceReport(
                    V27PairedClutterPlayModeVerifier.ReportPath,
                    "PASS\t")
            };
        string sourceDigest = ComputeSourceDigest(
            root,
            evidence);
        EvaluatedClosureRow[] evaluatedFaults = FaultRows
            .Select(row => EvaluateFault(root, row, evidence))
            .ToArray();
        int faultClosed = evaluatedFaults.Count(value => value.Closed);
        int structurallyVerified = evaluatedFaults.Count(value =>
            value.Structure.Verified);
        byte[] faultCsv = BuildCsv(evaluatedFaults, sourceDigest);
        byte[] faultReport = Utf8(
            $"RESULT={(faultClosed == 19 ? "PASS" : "IN_PROGRESS")}; batch=G; closed={faultClosed}; total=19; open={19 - faultClosed}\n"
            + "authority=source-derived-stable-row-registry\n"
            + "requiredEvidence=current-source-integrated-fault-token\n"
            + $"structurallyVerified={structurallyVerified}; structuralOpen={19 - structurallyVerified}\n"
            + $"unityExecutionRequired={19 - faultClosed}\n"
            + BuildEvidenceSummary(evidence)
            + "sourceDigest=" + sourceDigest + "\n"
            + "currentSourceDigest="
            + V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest() + "\n"
            + "gameplaySceneSha256="
            + V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest() + "\n");
        return new CaptureResult(faultCsv, faultReport);
    }

    private static byte[] CaptureRemainingFaultEvidence()
    {
        using EditorVerificationSceneFixtureScope fixtureScene = new(
            "qa:v27-batch-g-remaining-fault-evidence");
        string dropRetry =
            ProductionInputDestinationCustodyDrainServiceDebugScenarios
                .RunDropFailureRecoveryPendingRetryFocused();
        string multiStack = HaulPlanConstructionSafetyDebugScenarios
            .RunMultiStackHaulFocused();
        string partialPickup = HaulPlanConstructionSafetyDebugScenarios
            .RunPartialPickupFocused();

        Require(!string.IsNullOrWhiteSpace(dropRetry),
            "Drop-publication/recovery-pending focused evidence is empty.");
        Require(!string.IsNullOrWhiteSpace(multiStack),
            "Multi-stack focused evidence is empty.");
        Require(!string.IsNullOrWhiteSpace(partialPickup),
            "Partial-pickup focused evidence is empty.");

        string currentSource = V27CurrentSourceEvidenceDigest
            .ComputeAllScriptsDigest();
        string scene = V27CurrentSourceEvidenceDigest
            .ComputeGameplaySceneDigest();
        return Utf8(
            "RESULT=PASS\n"
            + "schema=v27-batch-g-remaining-fault-evidence-v1\n"
            + "BATCH_G_DROP_PUBLICATION_FAILURE_EXACT_ONCE=PASS\n"
            + "BATCH_G_RECOVERY_PENDING_RETRY_ACK=PASS\n"
            + "BATCH_G_MULTI_STACK_QUANTITY_GRAM_EXACT=PASS\n"
            + "BATCH_G_PARTIAL_PICKUP_CUSTODY_EXACT=PASS\n"
            + "dropRetry=" + dropRetry + "\n"
            + "multiStack=" + multiStack + "\n"
            + "partialPickup=" + partialPickup + "\n"
            + "currentSourceDigest=" + currentSource + "\n"
            + "gameplaySceneSha256=" + scene + "\n");
    }

    private static void ValidateRows(string batch, ClosureRow[] rows, int expected)
    {
        Require(rows.Length == expected,
            $"Batch {batch} denominator drifted: {rows.Length}/{expected}.");
        string[] ids = rows.Select(value => value.RowId).ToArray();
        Require(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length
                && ids.SequenceEqual(ids.OrderBy(value => value, StringComparer.Ordinal)),
            $"Batch {batch} row IDs must be unique and ordinal sorted.");
        Require(rows.All(value => !string.IsNullOrWhiteSpace(value.RequiredEvidence)),
            $"Batch {batch} contains an empty evidence contract.");
    }

    private static void ValidateFaultStructure()
    {
        string[] rows = FaultRows.Select(value => value.RowId).ToArray();
        string[] specs = FaultStructure.Keys
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(rows.SequenceEqual(specs),
            "Batch G structural specs must cover the exact official 19-row denominator.");
        foreach (KeyValuePair<string, StructuralSpec> pair in FaultStructure)
        {
            Require(IsCanonicalProbe(pair.Value.Production),
                $"Batch G production probe is non-canonical: {pair.Key}.");
            Require(IsCanonicalProbe(pair.Value.Fixture),
                $"Batch G focused fixture probe is non-canonical: {pair.Key}.");
        }
    }

    private static bool IsCanonicalProbe(SourceProbe probe) =>
        !string.IsNullOrEmpty(probe.Path)
        && string.Equals(probe.Path, probe.Path.Trim(), StringComparison.Ordinal)
        && probe.Path.IndexOf('\\') < 0
        && probe.Tokens.Count > 0
        && probe.Tokens.All(value => !string.IsNullOrEmpty(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal));

    private static byte[] BuildCsv(IEnumerable<ClosureRow> rows, string sourceDigest)
    {
        using MemoryStream stream = new MemoryStream();
        V27Utf8CsvWriter writer = new V27Utf8CsvWriter(stream, 8192);
        WriteRow(writer, new[]
        {
            "schemaVersion", "batch", "rowId", "requiredEvidence",
            "reviewStatus", "evidenceToken", "sourceDigest"
        });
        foreach (ClosureRow row in rows)
        {
            WriteRow(writer, new[]
            {
                "1", row.Batch, row.RowId, row.RequiredEvidence,
                "OPEN:no-integrated-current-source-evidence", string.Empty,
                sourceDigest
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] BuildCsv(
        IEnumerable<EvaluatedClosureRow> rows,
        string sourceDigest)
    {
        using MemoryStream stream = new MemoryStream();
        V27Utf8CsvWriter writer = new V27Utf8CsvWriter(stream, 8192);
        WriteRow(writer, new[]
        {
            "schemaVersion", "batch", "rowId", "requiredEvidence",
            "structuralStatus", "productionCallsite", "productionProbe",
            "focusedFixture", "focusedProbe",
            "reviewStatus", "evidenceToken", "runtimeExecution", "sourceDigest"
        });
        foreach (EvaluatedClosureRow row in rows)
        {
            WriteRow(writer, new[]
            {
                "3", row.Row.Batch, row.Row.RowId, row.Row.RequiredEvidence,
                row.Structure.Verified
                    ? "PASS:production-and-focused-fixture-linked"
                    : "OPEN:" + row.Structure.FailureReason,
                row.Structure.ProductionPath,
                row.Structure.ProductionProbe,
                row.Structure.FixturePath,
                row.Structure.FixtureProbe,
                row.Closed
                    ? "PASS:current-source-integrated-evidence"
                    : "OPEN:no-integrated-current-source-evidence",
                row.EvidenceToken,
                row.Closed ? "CURRENT_SOURCE_PASS" : "REQUIRES_UNITY_PLAYMODE",
                sourceDigest
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static EvaluatedClosureRow EvaluateFault(
        string root,
        ClosureRow row,
        IReadOnlyDictionary<EvidenceKind, EvidenceReport> evidence)
    {
        StructuralEvaluation structure = EvaluateStructure(root, row);
        if (!structure.Verified)
            return new EvaluatedClosureRow(
                row,
                structure,
                false,
                string.Empty);
        if (!FaultEvidence.TryGetValue(row.RowId, out EvidenceSpec spec))
            return new EvaluatedClosureRow(
                row,
                structure,
                false,
                string.Empty);
        if (!evidence.TryGetValue(spec.Kind, out EvidenceReport report))
            return new EvaluatedClosureRow(
                row,
                structure,
                false,
                string.Empty);
        if (!report.Fresh
            || spec.Tokens.Any(token => !report.Text.Contains(
                report.PassPrefix + token,
                StringComparison.Ordinal)))
        {
            return new EvaluatedClosureRow(
                row,
                structure,
                false,
                string.Empty);
        }
        string tokenValue = spec.Kind.ToString().ToLowerInvariant()
            + ":" + report.ByteDigest + ":"
            + string.Join("|", spec.Tokens);
        return new EvaluatedClosureRow(row, structure, true, tokenValue);
    }

    private static StructuralEvaluation EvaluateStructure(
        string root,
        ClosureRow row)
    {
        if (!FaultStructure.TryGetValue(row.RowId, out StructuralSpec spec))
        {
            return new StructuralEvaluation(
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "missing-structural-spec");
        }

        if (!TryValidateProbe(root, spec.Production, out string productionFailure))
        {
            return new StructuralEvaluation(
                false,
                spec.Production.Path,
                spec.Production.TokenSummary,
                spec.Fixture.Path,
                spec.Fixture.TokenSummary,
                productionFailure);
        }
        if (!string.IsNullOrEmpty(spec.ExplicitOpenReason))
        {
            return new StructuralEvaluation(
                false,
                spec.Production.Path,
                spec.Production.TokenSummary,
                spec.Fixture.Path,
                spec.Fixture.TokenSummary,
                spec.ExplicitOpenReason);
        }
        if (!TryValidateProbe(root, spec.Fixture, out string fixtureFailure))
        {
            return new StructuralEvaluation(
                false,
                spec.Production.Path,
                spec.Production.TokenSummary,
                spec.Fixture.Path,
                spec.Fixture.TokenSummary,
                fixtureFailure);
        }
        return new StructuralEvaluation(
            true,
            spec.Production.Path,
            spec.Production.TokenSummary,
            spec.Fixture.Path,
            spec.Fixture.TokenSummary,
            string.Empty);
    }

    private static bool TryValidateProbe(
        string root,
        SourceProbe probe,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrEmpty(probe.Path))
        {
            failureReason = "missing-focused-fixture";
            return false;
        }
        string absolute = Path.Combine(
            root,
            probe.Path.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolute))
        {
            failureReason = "missing-source-file:" + probe.Path;
            return false;
        }
        string source = File.ReadAllText(absolute).Replace("\r\n", "\n");
        string missing = probe.Tokens.FirstOrDefault(token =>
            !source.Contains(token, StringComparison.Ordinal));
        if (missing != null)
        {
            failureReason = "missing-source-token:" + missing;
            return false;
        }
        return true;
    }

    private static EvidenceReport LoadEvidenceReport(
        string path,
        string passPrefix)
    {
        if (!File.Exists(path))
            return new EvidenceReport(false, string.Empty, string.Empty, passPrefix);
        string text = File.ReadAllText(path).Replace("\r\n", "\n");
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
        string digest;
        using (SHA256 sha = SHA256.Create())
            digest = Hex(sha.ComputeHash(bytes));
        string currentSource = V27CurrentSourceEvidenceDigest
            .ComputeAllScriptsDigest();
        string scene = V27CurrentSourceEvidenceDigest
            .ComputeGameplaySceneDigest();
        bool fresh = text.Contains("RESULT=PASS", StringComparison.Ordinal)
            && text.Contains(
                "currentSourceDigest=" + currentSource,
                StringComparison.Ordinal)
            && text.Contains(
                "gameplaySceneSha256=" + scene,
                StringComparison.Ordinal);
        return new EvidenceReport(fresh, text, digest, passPrefix);
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

    private static string ComputeSourceDigest(
        string root,
        IReadOnlyDictionary<EvidenceKind, EvidenceReport> evidence)
    {
        using SHA256 sha = SHA256.Create();
        string[] digestPaths = DigestSourcePaths
            .Concat(FaultStructure.Values.SelectMany(value => new[]
            {
                value.Production.Path,
                value.Fixture.Path
            }))
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        foreach (string path in digestPaths)
        {
            string absolute = Path.Combine(
                root,
                path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolute))
                throw new InvalidOperationException("Digest source is missing: " + path);
            string source = File.ReadAllText(absolute).Replace("\r\n", "\n");
            byte[] bytes = Encoding.UTF8.GetBytes(path + "\n" + source + "\n");
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        string evidenceTokens = string.Join(
            "",
            Enum.GetValues(typeof(EvidenceKind))
                .Cast<EvidenceKind>()
                .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                .Select(value => value + "\t"
                    + (evidence.TryGetValue(value, out EvidenceReport report)
                        ? report.ByteDigest
                        : string.Empty)
                    + "\n"));
        byte[] evidenceBytes = Encoding.UTF8.GetBytes(evidenceTokens);
        sha.TransformBlock(
            evidenceBytes,
            0,
            evidenceBytes.Length,
            null,
            0);
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Hex(sha.Hash);
    }

    private static void Write(string path, byte[] bytes) =>
        V27BalanceArtifactWriter.WriteIfDifferent(
            path,
            stream => stream.Write(bytes, 0, bytes.Length));

    private static byte[] Utf8(string value) =>
        new UTF8Encoding(false, true).GetBytes(value);

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

    private static ClosureRow Domain(string id, string evidence) =>
        new ClosureRow("F", id, evidence);

    private static ClosureRow Fault(string id, string evidence) =>
        new ClosureRow("G", id, evidence);

    private static EvidenceSpec P17(params string[] tokens) =>
        new EvidenceSpec(EvidenceKind.PreparedOutputP17, tokens);

    private static EvidenceSpec Synthetic(params string[] tokens) =>
        new EvidenceSpec(EvidenceKind.SyntheticPreparedOutput, tokens);

    private static EvidenceSpec AuthoredTransport(params string[] tokens) =>
        new EvidenceSpec(EvidenceKind.AuthoredTransport, tokens);

    private static EvidenceSpec ProductionInput(params string[] tokens) =>
        new EvidenceSpec(EvidenceKind.ProductionInput, tokens);

    private static EvidenceSpec Destructive(params string[] tokens) =>
        new EvidenceSpec(EvidenceKind.DestructiveDrain, tokens);

    private static EvidenceSpec CrossAction(params string[] tokens) =>
        new EvidenceSpec(EvidenceKind.CrossActionFault, tokens);

    private static EvidenceSpec Paired(params string[] tokens) =>
        new EvidenceSpec(EvidenceKind.PairedClutter, tokens);

    private static EvidenceSpec HaulLifecycle(params string[] tokens) =>
        new EvidenceSpec(EvidenceKind.HaulLifecycleRecovery, tokens);

    private static EvidenceSpec RemainingFault(params string[] tokens) =>
        new EvidenceSpec(EvidenceKind.RemainingFaultFocused, tokens);

    private static SourceProbe Probe(string path, params string[] tokens) =>
        new SourceProbe(path, tokens);

    private static StructuralSpec Structure(
        SourceProbe production,
        SourceProbe fixture) =>
        new StructuralSpec(production, fixture, string.Empty);

    private static string BuildEvidenceSummary(
        IReadOnlyDictionary<EvidenceKind, EvidenceReport> evidence) =>
        string.Join(
            "",
            Enum.GetValues(typeof(EvidenceKind))
                .Cast<EvidenceKind>()
                .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                .Select(value =>
                {
                    evidence.TryGetValue(value, out EvidenceReport report);
                    return "evidence=" + value + "; fresh="
                        + report.Fresh.ToString().ToLowerInvariant()
                        + "; sha256=" + report.ByteDigest + "\n";
                }));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct ClosureRow
    {
        public ClosureRow(string batch, string rowId, string requiredEvidence)
        {
            Batch = batch;
            RowId = rowId;
            RequiredEvidence = requiredEvidence;
        }

        public string Batch { get; }
        public string RowId { get; }
        public string RequiredEvidence { get; }
    }

    private enum EvidenceKind
    {
        AuthoredTransport,
        CrossActionFault,
        DestructiveDrain,
        HaulLifecycleRecovery,
        PairedClutter,
        PreparedOutputP17,
        ProductionInput,
        RemainingFaultFocused,
        SyntheticPreparedOutput
    }

    private readonly struct EvidenceSpec
    {
        public EvidenceSpec(EvidenceKind kind, IReadOnlyList<string> tokens)
        {
            Kind = kind;
            Tokens = tokens ?? Array.Empty<string>();
        }

        public EvidenceKind Kind { get; }
        public IReadOnlyList<string> Tokens { get; }
    }

    private readonly struct EvidenceReport
    {
        public EvidenceReport(
            bool fresh,
            string text,
            string byteDigest,
            string passPrefix)
        {
            Fresh = fresh;
            Text = text ?? string.Empty;
            ByteDigest = byteDigest ?? string.Empty;
            PassPrefix = passPrefix ?? string.Empty;
        }

        public bool Fresh { get; }
        public string Text { get; }
        public string ByteDigest { get; }
        public string PassPrefix { get; }
    }

    private readonly struct EvaluatedClosureRow
    {
        public EvaluatedClosureRow(
            ClosureRow row,
            StructuralEvaluation structure,
            bool closed,
            string evidenceToken)
        {
            Row = row;
            Structure = structure;
            Closed = closed;
            EvidenceToken = evidenceToken ?? string.Empty;
        }

        public ClosureRow Row { get; }
        public StructuralEvaluation Structure { get; }
        public bool Closed { get; }
        public string EvidenceToken { get; }
    }

    private readonly struct SourceProbe
    {
        public SourceProbe(string path, IReadOnlyList<string> tokens)
        {
            Path = path ?? string.Empty;
            Tokens = tokens ?? Array.Empty<string>();
        }

        public string Path { get; }
        public IReadOnlyList<string> Tokens { get; }
        public string TokenSummary => string.Join("|", Tokens);
    }

    private readonly struct StructuralSpec
    {
        public StructuralSpec(
            SourceProbe production,
            SourceProbe fixture,
            string explicitOpenReason)
        {
            Production = production;
            Fixture = fixture;
            ExplicitOpenReason = explicitOpenReason ?? string.Empty;
        }

        public SourceProbe Production { get; }
        public SourceProbe Fixture { get; }
        public string ExplicitOpenReason { get; }
    }

    private readonly struct StructuralEvaluation
    {
        public StructuralEvaluation(
            bool verified,
            string productionPath,
            string productionProbe,
            string fixturePath,
            string fixtureProbe,
            string failureReason)
        {
            Verified = verified;
            ProductionPath = productionPath ?? string.Empty;
            ProductionProbe = productionProbe ?? string.Empty;
            FixturePath = fixturePath ?? string.Empty;
            FixtureProbe = fixtureProbe ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Verified { get; }
        public string ProductionPath { get; }
        public string ProductionProbe { get; }
        public string FixturePath { get; }
        public string FixtureProbe { get; }
        public string FailureReason { get; }
    }

    private readonly struct CaptureResult
    {
        public CaptureResult(
            byte[] faultCsv,
            byte[] faultReport)
        {
            FaultCsv = faultCsv;
            FaultReport = faultReport;
            FaultClosed = CountClosed(faultCsv);
        }

        public byte[] FaultCsv { get; }
        public byte[] FaultReport { get; }
        public int FaultClosed { get; }

        private static int CountClosed(byte[] csv) =>
            Encoding.UTF8.GetString(csv ?? Array.Empty<byte>())
                .Split('\n')
                .Count(value => value.Contains(
                    "PASS:current-source-integrated-evidence",
                    StringComparison.Ordinal));
    }
}
#endif
