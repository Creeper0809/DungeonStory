#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

public static class CharacterAiEmergentCombinationCoverageDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/character-ai-emergent-combination-coverage.txt";

    private static readonly IReadOnlyDictionary<string, string> ActionFamilies =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(AIDrink)] = "atomic-consumable",
            [nameof(AISubstanceUse)] = "atomic-consumable",
            [nameof(AIEat)] = "facility-visit",
            [nameof(AIFacilityRoleAction)] = "facility-visit",
            [nameof(AIRest)] = "facility-visit",
            [nameof(AIShopping)] = "facility-visit",
            [nameof(AIExitDungeon)] = "destinationless",
            [nameof(AILookAround)] = "destinationless",
            [nameof(AIWait)] = "destinationless",
            [nameof(AIHaul)] = "haul-committed",
            [nameof(AIHunt)] = "hunt-pursuit",
            [nameof(AIRescue)] = "rescue-carry",
            [nameof(AIWork)] = "work-routine",
            [nameof(AIPrimitiveBucketWash)] = "external-primitive",
            [nameof(AIPrimitiveFieldMeal)] = "external-primitive",
            [nameof(AIPrimitiveFloorRest)] = "external-primitive",
            [nameof(AIPrimitiveLatrine)] = "external-primitive",
            [nameof(AIDesperateRelief)] = "external-breakdown",
            [nameof(AIDesperateDrink)] = "external-breakdown",
            [nameof(AIDesperateEat)] = "external-breakdown",
            [nameof(AICollapse)] = "external-breakdown",
            [nameof(AIViolentBreakdown)] = "external-breakdown"
        };

    private static readonly string[] PerturbationFamilies =
    {
        "alert-context",
        "lifecycle-ineligible",
        "target-or-destination-loss",
        "path-or-topology-change",
        "resource-or-lease-loss",
        "save-restore-rebind",
        "concurrent-owner-or-contender"
    };

    private static readonly CoverageRow[] Rows =
    {
        R("P01", "atomic-consumable", "alert-context", Routine(),
            "ROUTINE_LOCKDOWN_DRINK_LIVE_BEFORE_ALERT",
            "ROUTINE_LOCKDOWN_ATOMIC_DRINK_HANDOFF_TO_GUARD",
            "ROUTINE_LOCKDOWN_NO_DUAL_OWNER"),
        R("P02", "atomic-consumable", "resource-or-lease-loss", Consumable(),
            "DRINK_SOURCE_LOSS_RUNNING_LEASE",
            "DRINK_SOURCE_LOSS_TYPED_FAILED",
            "DRINK_LEASE_INVALID_TYPED_FAILED"),
        R("P03", "atomic-consumable", "concurrent-owner-or-contender", SelfCare(),
            "DRINK_SELECTED_ACTION_EPOCH_OWNED",
            "DRINK_NO_EXTERNAL_OWNER_OVERLAP",
            "DRINK_LIFECYCLE_CONSERVED"),

        R("P04", "facility-visit", "path-or-topology-change", FaultRecovery(),
            "row=core:repath;started=True;completed=True;result=PASS",
            "row=core:no-path;started=True;completed=True;result=PASS"),
        R("P05", "facility-visit", "target-or-destination-loss", FaultRecovery(),
            "row=facility-shared:approach;started=True;completed=True;result=PASS",
            "row=facility-shared:queue;started=True;completed=True;result=PASS",
            "row=facility-shared:interaction;started=True;completed=True;result=PASS"),
        R("P06", "facility-visit", "resource-or-lease-loss", Consumable(),
            "EAT_SOURCE_LOSS_RUNNING_MEAL_PLAN",
            "EAT_SPOIL_TYPED_FAILED",
            "EAT_LEASE_INVALID_TYPED_FAILED"),
        R("P07", "facility-visit", "alert-context", FacilityAlert(),
            "FACILITY_ALERT_VISIT_LIVE_BEFORE_RED",
            "FACILITY_ALERT_RED_COMMITTED_DURING_VISIT",
            "FACILITY_ALERT_NO_GUARD_EXECUTION_OVERLAP",
            "FACILITY_ALERT_GUARD_AFTER_VISIT_TERMINAL"),

        R("P08", "destinationless", "path-or-topology-change", FaultRecovery(),
            "row=destinationless:look_around:recovery;started=True;completed=True;result=PASS",
            "row=destinationless:wait:starvation;started=True;completed=True;result=PASS",
            "row=destinationless:exit_dungeon:starvation;started=True;completed=True;result=PASS"),

        R("P09", "external-breakdown", "path-or-topology-change", PerfectStorm(),
            "PERFECT_STORM_BREAKDOWN_BT_MOVEMENT_STARTED",
            "PERFECT_STORM_DYNAMIC_WALL_PLACED",
            "PERFECT_STORM_LIVE_ROUTE_INVALIDATED"),
        R("P10", "external-primitive", "target-or-destination-loss", FaultRecovery(),
            "row=primitive:bucket-wash:target-lost;started=True;completed=True;result=PASS",
            "row=primitive:latrine:target-invalidated;started=True;completed=True;result=PASS"),
        R("P11", "external-primitive", "resource-or-lease-loss", FaultRecovery(),
            "row=primitive:field-meal:commit-loss;started=True;completed=True;result=PASS"),
        R("P12", "external-intent", "alert-context", ExternalAlert(),
            "EXTERNAL_ALERT_LEASE_LIVE_BEFORE_RED",
            "EXTERNAL_ALERT_POLICY_GATE_BOUND_DURING_LEASE",
            "EXTERNAL_ALERT_NO_GUARD_EXECUTION_OVERLAP",
            "EXTERNAL_ALERT_GUARD_STARTED_AFTER_EXTERNAL_TERMINAL"),
        R("P13", "external-intent", "lifecycle-ineligible", CaptivityWildlife(),
            "TRANSPORT_SUCCESS_ATOMIC_PICKUP_OWNERSHIP",
            "TRANSPORT_DOWNED_TERMINAL"),

        R("P14", "work-routine", "alert-context", Alarm(),
            "WORK_SUSPENDED_AT_CHECKPOINT",
            "ORIGINAL_WORK_RETURNED",
            "GREEN_RETURN_OWNERSHIP_CLEAN"),
        R("P15", "work-routine", "lifecycle-ineligible", Lifecycle(),
            "rows=Downed,Dead,Despawned,Disabled,Destroyed",
            "action=5/5; movement=5/5; ownership=5/5"),
        R("P16", "work-routine", "target-or-destination-loss", Alarm(),
            "SUSPENDED_TARGET_DESTROYED",
            "DESTROYED_WORK_JOURNAL_ABANDONED",
            "DESTROYED_WORK_NOT_FALSELY_RESTORED"),
        R("P17", "work-routine", "path-or-topology-change", WorkMatrix(),
            "RESULT=PASS; rows=20; passed=20; blocked=0; failed=0",
            "PASS\twork:perform\t",
            "PASS\twork:dismantle\t"),
        R("P18", "work-routine", "resource-or-lease-loss", Physical(),
            "MATERIAL_REPAIR_INPUTS_DELIVERED",
            "MATERIAL_REPAIR_NO_DUPLICATE_REQUEST",
            "MATERIAL_REPAIR_PRESERVES_INSTANCE_AND_MATERIAL"),
        R("P19", "work-routine", "save-restore-rebind", Alarm(),
            "INLINE_PROGRESS_SAVE_ROUNDTRIP",
            "INLINE_PROGRESS_RESUMED_NOT_RESTARTED",
            "INLINE_WORK_COMPLETED_ONCE_AFTER_RESUME"),

        R("P20", "critical-work", "alert-context", PerfectStorm(),
            "PERFECT_STORM_CRITICAL_SURGERY_PROTECTED_FROM_INVASION",
            "PERFECT_STORM_BOUNDED_LIVENESS"),
        R("P21", "critical-work", "resource-or-lease-loss", Surgery(),
            "SURGERY_MATERIALS_DELIVERED_BY_AI_HAUL",
            "SURGERY_REPEATED_MATERIAL_POLL_NO_DUPLICATE",
            "SURGERY_MATERIAL_DESTINATION_CLAIM_REVOKED_AFTER_COMPLETE"),
        R("P22", "critical-work", "save-restore-rebind", Surgery(),
            "SURGERY_CURRENT_SAVE_LIVE_CLINICAL_STAGE",
            "SURGERY_CURRENT_RESTORE_STATE_PROGRESS_EXACT",
            "SURGERY_CURRENT_RESTORE_NO_TRANSIENT_OWNER",
            "SURGERY_CURRENT_RESTORE_MATERIAL_CONSERVATION",
            "SURGERY_CURRENT_RESTORE_AIWORK_RESUMED_EXACT_ONCE"),

        R("P23", "haul-committed", "target-or-destination-loss", CrossAction(),
            "haul-source-despawn: stale pickup performed no late commit",
            "haul-destination-destroy: carry recovered and coroutine terminal"),
        R("P24", "haul-committed", "resource-or-lease-loss", CrossAction(),
            "haul-source-quantity-shrink: lease released exactly once",
            "haul-source-quantity-shrink: quantity conserved across injected physical loss"),
        R("P25", "haul-committed", "save-restore-rebind", SaveLoad(),
            "HAUL_SAVE_MID_PICKUP_COMMITTED",
            "HAUL_SAVE_RESTORE_1_INTENT_BOUND_BEFORE_AI_WAKE",
            "HAUL_SAVE_RESTORE_2_BRAIN_AIHAUL_EXACT_ONCE",
            "HAUL_SAVE_REPEATED_RESTORE_CONSERVATION_EXACT"),
        R("P26", "haul-committed", "concurrent-owner-or-contender", Physical(),
            "CONSTRUCTION_STOCK_WITHDRAWN_ON_PICKUP",
            "CONSTRUCTION_READY_AFTER_PHYSICAL_DELIVERY",
            "MATERIAL_REPAIR_NO_DUPLICATE_REQUEST"),
        R("P27", "haul-committed", "alert-context", HaulAlertCasualty(),
            "HAUL_ALERT_PICKUP_COMMITTED_THROUGH_BRAIN",
            "HAUL_ALERT_COMMITTED_DESTINATION_EXACT",
            "HAUL_ALERT_RED_BOUND_WHILE_PICKUP_COMMITTED",
            "HAUL_ALERT_NO_HAUL_GUARD_OWNER_OVERLAP"),
        R("P28", "haul-committed", "lifecycle-ineligible", HaulAlertCasualty(),
            "HAUL_ALERT_DOWNED_SYNCHRONOUS_EXACT_TERMINAL",
            "HAUL_ALERT_PHYSICAL_QUANTITY_CONSERVED_ON_CANCEL",
            "HAUL_ALERT_DOWNED_GATE_RETIRED",
            "HAUL_ALERT_RUNTIME_AND_PHYSICAL_CONSERVED"),

        R("P29", "rescue-carry", "target-or-destination-loss", CrossAction(),
            "rescue-patient-death: coroutine terminal without late treatment",
            "rescue-bed-destroy: carry released and late treatment commit is zero"),
        R("P30", "rescue-carry", "resource-or-lease-loss", CrossAction(),
            "rescue-medicine-loss: no treatment progress or late supply commit",
            "rescue-medicine-loss: medical ownership released exactly once"),
        R("P31", "rescue-carry", "save-restore-rebind", CrossAction(),
            "rescue-save-load: medical aggregate restored through V18 transaction boundary",
            "rescue-patient-despawn/save-load: coroutine and reservation terminal"),
        R("P32", "rescue-carry", "alert-context", RescueAlertRescuerDowned(),
            "RESCUE_ALERT_PHYSICAL_CARRY_LIVE_THROUGH_BRAIN",
            "RESCUE_ALERT_RED_BOUND_WHILE_CARRY_LIVE",
            "RESCUE_ALERT_NO_RESCUE_GUARD_OWNER_OVERLAP"),
        R("P33", "rescue-carry", "lifecycle-ineligible", RescueAlertRescuerDowned(),
            "RESCUE_ALERT_RESCUER_DOWNED_EXACT_TERMINAL",
            "RESCUE_ALERT_MEDICAL_ORDERS_CONSERVED",
            "RESCUE_ALERT_BOTH_MEDICAL_RECOVERIES_CONVERGED",
            "RESCUE_ALERT_RUNTIME_GATE_CONSERVED"),

        R("P34", "hunt-pursuit", "target-or-destination-loss", CrossAction(),
            "hunt-prey-despawn: failed terminal without carcass late commit"),
        R("P35", "hunt-pursuit", "path-or-topology-change", CrossAction(),
            "hunt-path-invalidation: no-path terminal without late hit"),
        R("P36", "hunt-pursuit", "lifecycle-ineligible", CrossAction(),
            "hunt-hunter-downed: reservation released and no late hit",
            "hunt-hunter-dead: reservation released and no late hit"),
        R("P37", "hunt-pursuit", "alert-context", HuntAlertTopologyLoss(),
            "HUNT_ALERT_PURSUIT_LIVE_THROUGH_BRAIN",
            "HUNT_ALERT_RED_AND_TOPOLOGY_COLLISION",
            "HUNT_ALERT_NO_HUNT_GUARD_OWNER_OVERLAP",
            "HUNT_ALERT_GUARD_AFTER_HUNT_TERMINAL",
            "HUNT_ALERT_FINAL_OWNERSHIP_CONVERGED"),

        R("P38", "captivity-transport", "target-or-destination-loss", CaptivityWildlife(),
            "TRANSPORT_SOURCE_LOSS_TERMINAL",
            "TRANSPORT_PEN_DESTROY_TERMINAL"),
        R("P39", "captivity-transport", "path-or-topology-change", CaptivityWildlife(),
            "TRANSPORT_NOPATH_TERMINAL"),
        R("P40", "captivity-transport", "lifecycle-ineligible", CaptivityWildlife(),
            "TRANSPORT_DOWNED_TERMINAL"),
        R("P41", "captivity-transport", "alert-context", CaptivityWildlife(),
            "TRANSPORT_ALERT_EXTERNAL_LEASE_RED_BOUND",
            "TRANSPORT_ALERT_NO_GUARD_OVERLAP",
            "TRANSPORT_ALERT_CARRIER_DOWNED_EXACT_TERMINAL",
            "TRANSPORT_ALERT_ROLLBACK_PHYSICAL_CONVERGED",
            "TRANSPORT_ALERT_RESPONSE_GREEN_CONVERGED"),
        R("P42", "captivity-transport", "concurrent-owner-or-contender", CaptivityWildlife(),
            "TRANSPORT_CONTENDER_INJECTED_AFTER_DESTINATION_RESOLVED",
            "TRANSPORT_CONTENDER_TYPED_ROLLBACK",
            "TRANSPORT_CONTENDER_PHYSICAL_CONSERVATION",
            "TRANSPORT_CONTENDER_NO_PENNED_DIVERGENCE"),

        R("P43", "emergency-responder", "lifecycle-ineligible", Responder(),
            "RESPONDER_CASUALTY_SYNCHRONOUS_LIFECYCLE_RELEASE",
            "RESPONDER_CASUALTY_REPLACEMENT_GUARD_SAME_EPOCH",
            "RESPONDER_CASUALTY_NO_DOWNED_REACQUIRE"),
        R("P44", "emergency-responder", "alert-context", Alarm(),
            "REESCALATION_CARRIES_RESPONDER_JOURNAL_AND_GATE",
            "SAME_EPOCH_RESPONSE_TYPE_RETARGET",
            "GREEN_RETURN_QUEUE_REESCALATION_CARRIED_ONCE"),
        R("P45", "emergency-responder", "save-restore-rebind", Alarm(),
            "INLINE_PROGRESS_SAVE_ROUNDTRIP",
            "SUSPENSION_JOURNAL_CLEARED_ON_RETURN"),

        R("P46", "selected-action", "lifecycle-ineligible", Lifecycle(),
            "rows=Downed,Dead,Despawned,Disabled,Destroyed",
            "cleanup=exactly-once; second-cleanup=idempotent; late-commit=0"),
        R("P47", "selected-action", "save-restore-rebind", SaveLoad(),
            "character save contract excludes transient action/path/coroutine ownership",
            "replacement actor acquired one fresh execution owner",
            "transient movement coroutine was not serialized or rebound"),
        R("P48", "reservation", "concurrent-owner-or-contender", Consumable(),
            "DRINK_SOURCE_LOSS_FOREIGN_LEASE_PRESERVED",
            "EAT_LEASE_INVALID_FOREIGN_LEASE_PRESERVED",
            "SUBSTANCE_LEASE_INVALID_FOREIGN_LEASE_PRESERVED"),

        R("T01", "critical-work+external-breakdown", "path-change+alert", PerfectStorm(),
            "PERFECT_STORM_SURGERY_LIVE",
            "PERFECT_STORM_LIVE_ROUTE_INVALIDATED",
            "PERFECT_STORM_CRITICAL_SURGERY_PROTECTED_FROM_INVASION",
            "PERFECT_STORM_RELEASED_BREAKER_BECOMES_GUARD"),
        R("T02", "emergency-responder+lifecycle", "medical-takeover", Responder(),
            "RESPONDER_CASUALTY_MEDICAL_ORDER_EXACTLY_ONE",
            "RESPONDER_CASUALTY_REPLACEMENT_GUARD_SAME_EPOCH",
            "RESPONDER_CASUALTY_MEDICAL_RECOVERY_CONVERGED"),
        R("T03", "atomic-consumable+physical-item", "alert-context", Routine(),
            "ROUTINE_LOCKDOWN_NO_PREMATURE_CONSUME",
            "ROUTINE_LOCKDOWN_ATOMIC_DRINK_CONSUMED_EXACTLY_ONCE",
            "ROUTINE_LOCKDOWN_NO_DUPLICATE_DRINK_AFTER_GREEN"),
        R("T04", "work-routine+alert", "target-destroy", Alarm(),
            "DESTROY_CASE_SUSPENDED",
            "SUSPENDED_TARGET_DESTROYED",
            "DESTROYED_WORK_JOURNAL_ABANDONED"),
        R("T05", "haul-committed+save", "duplicate-request", SaveLoad(),
            "HAUL_SAVE_RESTORE_1_DUPLICATE_REQUEST_ZERO",
            "HAUL_SAVE_RESTORE_1_PHYSICAL_QUANTITIES_UNCHANGED_BEFORE_AI_WAKE",
            "HAUL_SAVE_REPEATED_RESTORE_CONSERVATION_EXACT"),
        R("T06", "captivity-escape+alert", "lifecycle-or-path", CaptivityWildlife(),
            "ESCAPE_RUNNING_DOWNED_TERMINAL",
            "ESCAPE_NOPATH_TERMINAL",
            "ESCAPE_INVASION_RESPONSE_RELEASED"),
        R("T07", "haul-committed+alert", "hauler-downed", HaulAlertCasualty(),
            "HAUL_ALERT_RED_BOUND_WHILE_PICKUP_COMMITTED",
            "HAUL_ALERT_DOWNED_SYNCHRONOUS_EXACT_TERMINAL",
            "HAUL_ALERT_REPLACEMENT_GUARD_SAME_EPOCH",
            "HAUL_ALERT_NO_HAUL_GUARD_OWNER_OVERLAP",
            "HAUL_ALERT_FINAL_OWNERSHIP_CONVERGED"),
        R("T08", "rescue-carry+alert", "patient-or-rescuer-transition", RescueAlertRescuerDowned(),
            "RESCUE_ALERT_RED_BOUND_WHILE_CARRY_LIVE",
            "RESCUE_ALERT_RESCUER_DOWNED_EXACT_TERMINAL",
            "RESCUE_ALERT_REPLACEMENT_GUARD_SAME_EPOCH",
            "RESCUE_ALERT_NO_RESCUE_GUARD_OWNER_OVERLAP",
            "RESCUE_ALERT_FINAL_OWNERSHIP_CONVERGED"),
        R("T09", "hunt-pursuit+alert", "topology-change", HuntAlertTopologyLoss(),
            "HUNT_ALERT_PURSUIT_LIVE_THROUGH_BRAIN",
            "HUNT_ALERT_RED_AND_TOPOLOGY_COLLISION",
            "HUNT_ALERT_TOPOLOGY_NOPATH_EXACT_TERMINAL",
            "HUNT_ALERT_NO_OWNER_OVERLAP_THROUGH_HANDOFF",
            "HUNT_ALERT_RUNTIME_GATE_CONSERVED"),
        R("T10", "captivity-transport+alert", "carrier-downed-or-destination-occupied", CaptivityWildlife(),
            "TRANSPORT_ALERT_EXTERNAL_LEASE_RED_BOUND",
            "TRANSPORT_ALERT_NO_GUARD_OVERLAP",
            "TRANSPORT_ALERT_CARRIER_DOWNED_EXACT_TERMINAL",
            "TRANSPORT_ALERT_ROLLBACK_PHYSICAL_CONVERGED",
            "TRANSPORT_ALERT_FINAL_GREEN"),
        R("T11", "facility-visit+alert", "facility-destroy", FacilityAlert(),
            "FACILITY_ALERT_VISIT_OWNER_STABLE_AT_COLLISION",
            "FACILITY_ALERT_SYNCHRONOUS_OCCUPANCY_RELEASE",
            "FACILITY_ALERT_DESTROYED_TERMINAL_EXACTLY_ONCE",
            "FACILITY_ALERT_NO_LATE_INTERACTION",
            "FACILITY_ALERT_FINAL_OWNERSHIP_CONVERGED")
    };

    [MenuItem("DungeonStory/Debug/QA/Chaos/Capture Emergent Combination Coverage")]
    public static string CaptureReport()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Artifacts/QA");
        List<string> failures = new();
        StringBuilder report = new(32768);
        report.AppendLine("# Character AI Emergent Combination Coverage");
        report.AppendLine("revision=emergent-covering-array-v1-20260816");
        report.AppendLine("scope=current production action/ownership equivalence classes; pairwise plus selected shared-state 3-way rows");
        report.AppendLine("nonGoal=literal infinite frame interleavings and legacy-save migration");
        report.AppendLine();

        AuditActionUniverse(report, failures);
        report.AppendLine("## Perturbation families");
        foreach (string perturbation in PerturbationFamilies)
            report.AppendLine("PERTURBATION\t" + perturbation);
        report.AppendLine();

        int covered = 0;
        int uncovered = 0;
        int invalid = 0;
        report.AppendLine("## Pairwise and selected 3-way rows");
        report.AppendLine("status\tid\taction-family\tperturbation\tevidence\tdetail");
        foreach (CoverageRow row in Rows)
        {
            CoverageResolution resolution = Resolve(row);
            if (resolution.Status == "COVERED") covered++;
            else if (resolution.Status == "UNCOVERED") uncovered++;
            else invalid++;
            report.Append(resolution.Status).Append('\t')
                .Append(row.Id).Append('\t')
                .Append(row.ActionFamily).Append('\t')
                .Append(row.Perturbation).Append('\t')
                .Append(row.Evidence?.Path ?? "<none>").Append('\t')
                .AppendLine(OneLine(resolution.Detail));
        }

        bool exactIds = Rows.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count()
            == Rows.Length;
        if (!exactIds) failures.Add("duplicate coverage row IDs");
        if (uncovered > 0) failures.Add("uncovered=" + uncovered);
        if (invalid > 0) failures.Add("invalid=" + invalid);
        report.AppendLine();
        report.AppendLine($"SUMMARY rows={Rows.Length}; covered={covered}; uncovered={uncovered}; invalid={invalid}; exactIds={exactIds}");
        report.AppendLine($"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}; failures={failures.Count}; {string.Join(" | ", failures)}");
        File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        return $"EMERGENT_COMBINATION_COVERAGE={(failures.Count == 0 ? "PASS" : "FAIL")}; rows={Rows.Length}; covered={covered}; uncovered={uncovered}; invalid={invalid}; report={ReportPath}";
    }

    private static void AuditActionUniverse(StringBuilder report, ICollection<string> failures)
    {
        string[] concrete = typeof(AIActionSet).Assembly.GetTypes()
            .Where(type => typeof(AIActionSet).IsAssignableFrom(type)
                && !type.IsAbstract
                && type.Assembly.GetName().Name.IndexOf(
                    "Editor",
                    StringComparison.OrdinalIgnoreCase) < 0)
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expected = ActionFamilies.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] missing = expected.Except(concrete, StringComparer.Ordinal).ToArray();
        string[] unknown = concrete.Except(expected, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0) failures.Add("action-universe missing=" + string.Join(",", missing));
        if (unknown.Length > 0) failures.Add("action-universe unmapped=" + string.Join(",", unknown));

        report.AppendLine("## Action family authority");
        report.AppendLine($"ACTION_UNIVERSE expected={expected.Length}; actual={concrete.Length}; missing=[{string.Join(",", missing)}]; unmapped=[{string.Join(",", unknown)}]");
        foreach (IGrouping<string, KeyValuePair<string, string>> family in ActionFamilies
                     .GroupBy(pair => pair.Value, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            report.AppendLine("ACTION_FAMILY\t" + family.Key + "\t"
                + string.Join(",", family.Select(pair => pair.Key)
                    .OrderBy(name => name, StringComparer.Ordinal)));
        }
        report.AppendLine();
    }

    private static CoverageResolution Resolve(CoverageRow row)
    {
        if (row.Evidence == null)
            return new CoverageResolution("UNCOVERED", row.Requirement);
        if (!File.Exists(row.Evidence.Path))
            return new CoverageResolution("INVALID", "artifact missing");

        EvidenceSourceFreshness source = CharacterAiCoverageSourceInventory
            .CaptureEvidenceSourceFreshness(row.Evidence.VerifierType);
        if (!source.Resolved)
            return new CoverageResolution("INVALID", source.FailureReason);
        DateTime artifactUtc = File.GetLastWriteTimeUtc(row.Evidence.Path);
        if (artifactUtc < source.LatestWriteUtc)
        {
            return new CoverageResolution("INVALID",
                "stale artifactUtc=" + artifactUtc.ToString("O")
                + ";sourceUtc=" + source.LatestWriteUtc.ToString("O")
                + ";latest=" + source.LatestSourcePath);
        }

        string text = File.ReadAllText(row.Evidence.Path);
        if (text.IndexOf(row.Evidence.AggregateMarker, StringComparison.Ordinal) < 0)
            return new CoverageResolution("INVALID", "aggregate marker missing: " + row.Evidence.AggregateMarker);
        string[] missing = row.Markers
            .Where(marker => text.IndexOf(marker, StringComparison.Ordinal) < 0)
            .ToArray();
        if (missing.Length > 0)
            return new CoverageResolution("INVALID", "scope marker(s) missing: " + string.Join(",", missing));

        return new CoverageResolution("COVERED",
            "markers=" + string.Join("+", row.Markers)
            + ";artifactUtc=" + artifactUtc.ToString("O")
            + ";sourceUtc=" + source.LatestWriteUtc.ToString("O"));
    }

    private static CoverageRow R(
        string id,
        string actionFamily,
        string perturbation,
        ArtifactEvidence evidence,
        params string[] markers) =>
        new(id, actionFamily, perturbation, evidence, markers, string.Empty);

    private static CoverageRow U(
        string id,
        string actionFamily,
        string perturbation,
        string requirement) =>
        new(id, actionFamily, perturbation, null, Array.Empty<string>(), requirement);

    private static ArtifactEvidence FaultRecovery() => E(
        "CharacterAiFaultRecoveryPlayModeVerifier",
        "Artifacts/QA/character-ai-fault-recovery-playmode.txt",
        "RESULT=PASS");
    private static ArtifactEvidence CrossAction() => E(
        "CharacterAiCrossActionFaultPlayModeVerifier",
        "Artifacts/QA/character-ai-cross-action-fault-playmode.txt",
        "result=PASS");
    private static ArtifactEvidence Lifecycle() => E(
        "CharacterAiLifecycleFaultPlayModeVerifier",
        "Artifacts/QA/character-ai-lifecycle-fault-playmode.txt",
        "result=PASS");
    private static ArtifactEvidence Consumable() => E(
        "CharacterConsumableActionFaultPlayModeVerifier",
        "Artifacts/QA/character-consumable-action-fault-playmode.txt",
        "RESULT=PASS");
    private static ArtifactEvidence SelfCare() => E(
        "CharacterAiSelfCarePlayModeVerifier",
        "Artifacts/QA/character-ai-self-care-playmode.txt",
        "RESULT=PASS");
    private static ArtifactEvidence Alarm() => E(
        "CharacterAlarmResponsePlayModeVerifier",
        "Artifacts/QA/character-alarm-response-playmode.txt",
        "RESULT=PASS");
    private static ArtifactEvidence WorkMatrix() => E(
        "CharacterAiWorkTypeLiveMatrixPlayModeVerifier",
        "Artifacts/QA/character-ai-worktype-live-matrix.txt",
        "RESULT=PASS; rows=20; passed=20; blocked=0; failed=0");
    private static ArtifactEvidence SaveLoad() => E(
        "DungeonAiActionSaveLoadPlayModeVerifier",
        "Artifacts/QA/ai-mid-action-save-load-playmode.txt",
        "result=PASS");
    private static ArtifactEvidence Physical() => E(
        "PhysicalItemLogisticsPlayModeVerifier",
        "Artifacts/QA/physical-item-logistics-playmode-report.txt",
        "RESULT=PASS");
    private static ArtifactEvidence CaptivityWildlife() => E(
        "CaptivityWildlifeLifecyclePlayModeVerifier",
        "Artifacts/QA/captivity-wildlife-lifecycle-playmode.txt",
        "RESULT=PASS");
    private static ArtifactEvidence Surgery() => E(
        "SurgeryPlayModeVerifier",
        "Artifacts/QA/surgery-playmode-report.txt",
        "RESULT=PASS");
    private static ArtifactEvidence PerfectStorm() => E(
        "CharacterAiEmergentChaosPlayModeVerifier",
        "Artifacts/QA/character-ai-emergent-chaos-seed-271828.txt",
        "RESULT=PASS");
    private static ArtifactEvidence Routine() => E(
        "CharacterAiAdditionalChaosPlayModeVerifier",
        CharacterAiAdditionalChaosPlayModeVerifier.RoutineLockdownReportPath,
        "RESULT=PASS");
    private static ArtifactEvidence Responder() => E(
        "CharacterAiAdditionalChaosPlayModeVerifier",
        CharacterAiAdditionalChaosPlayModeVerifier.ResponderCasualtyReportPath,
        "RESULT=PASS");
    private static ArtifactEvidence ExternalAlert() => E(
        "CharacterAiAdditionalChaosPlayModeVerifier",
        CharacterAiAdditionalChaosPlayModeVerifier.ExternalIntentAlertReportPath,
        "RESULT=PASS");
    private static ArtifactEvidence FacilityAlert() => E(
        "CharacterAiAdditionalChaosPlayModeVerifier",
        CharacterAiAdditionalChaosPlayModeVerifier.FacilityAlertDestroyReportPath,
        "RESULT=PASS");
    private static ArtifactEvidence HaulAlertCasualty() => E(
        "CharacterAiAdditionalChaosPlayModeVerifier",
        CharacterAiAdditionalChaosPlayModeVerifier.HaulAlertCasualtyReportPath,
        "RESULT=PASS");
    private static ArtifactEvidence RescueAlertRescuerDowned() => E(
        "CharacterAiAdditionalChaosPlayModeVerifier",
        CharacterAiAdditionalChaosPlayModeVerifier.RescueAlertRescuerDownedReportPath,
        "RESULT=PASS");
    private static ArtifactEvidence HuntAlertTopologyLoss() => E(
        "CharacterAiAdditionalChaosPlayModeVerifier",
        CharacterAiAdditionalChaosPlayModeVerifier.HuntAlertTopologyLossReportPath,
        "RESULT=PASS");

    private static ArtifactEvidence E(string type, string path, string aggregate) =>
        new(type, path, aggregate);

    private static string OneLine(string value) =>
        (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();

    private sealed class ArtifactEvidence
    {
        public ArtifactEvidence(string verifierType, string path, string aggregateMarker)
        {
            VerifierType = verifierType ?? string.Empty;
            Path = path ?? string.Empty;
            AggregateMarker = aggregateMarker ?? string.Empty;
        }

        public string VerifierType { get; }
        public string Path { get; }
        public string AggregateMarker { get; }
    }

    private readonly struct CoverageRow
    {
        public CoverageRow(
            string id,
            string actionFamily,
            string perturbation,
            ArtifactEvidence evidence,
            string[] markers,
            string requirement)
        {
            Id = id ?? string.Empty;
            ActionFamily = actionFamily ?? string.Empty;
            Perturbation = perturbation ?? string.Empty;
            Evidence = evidence;
            Markers = markers ?? Array.Empty<string>();
            Requirement = requirement ?? string.Empty;
        }

        public string Id { get; }
        public string ActionFamily { get; }
        public string Perturbation { get; }
        public ArtifactEvidence Evidence { get; }
        public string[] Markers { get; }
        public string Requirement { get; }
    }

    private readonly struct CoverageResolution
    {
        public CoverageResolution(string status, string detail)
        {
            Status = status ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public string Status { get; }
        public string Detail { get; }
    }
}
#endif
