#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Headless, actual command/save regression for audit U2. No LLM or UI.</summary>
public static class GameplayLedgerAuditRemediationDebugScenarios
{
    public static void RunShopContinuationBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new create-only shop continuation output directory is required.");
        Directory.CreateDirectory(output);
        try
        {
            if (!ShopUseExecutionStateDebugScenarios.RunAll(log: true)
                || !ShopPurchaseOwnerSaveValidationDebugScenarios.RunAll(log: true)
                || !ServiceExecutionOwnerSaveValidationDebugScenarios.RunAll(log: true))
                throw new InvalidOperationException("Shop execution state regression failed.");
            RetailPurchaseRecoveryB0T04DebugScenarios.RunOrThrow();
            RetailPurchaseRecoveryB0T04DebugScenarios.RunBoundContinuationScenarios();
            if (!CustomerAiDebugScenarios.RunAll(true))
                throw new InvalidOperationException("Customer AI regression failed.");
            WriteReport(output, new JObject { ["passed"] = true, ["test"] = "shop-continuation-foundation",
                ["scope"] = "DTO validation and detached purchase owner joins; production purchase physical/outcome/revenue failures and bound cart module restore/replay; controlled customer AI fixture (including captive/free exit admission query); NOT whole-world Shop interaction continuation or real captivity integration",
                ["fullAuditClosure"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false });
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject { ["passed"] = false, ["failure"] = error.ToString() });
            throw;
        }
    }

    public static void RunServiceExecutionOwnerBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new create-only service owner output directory is required.");
        Directory.CreateDirectory(output);
        try
        {
            if (!ServiceExecutionOwnerSaveValidationDebugScenarios.RunAll(log: true)
                || !ServiceSessionOutcomeDebugScenarios.RunAll(log: true))
                throw new InvalidOperationException("Service execution owner regression failed.");
            string treatment = SurvivalDebugScenarios.RunTreatmentBridgeForServiceOwnerVerification();
            string fuel = SurvivalDebugScenarios.RunFacilityFuelDeliveryFocused();
            WriteReport(output, new JObject {
                ["passed"] = true, ["test"] = "service-execution-owners",
                ["scope"] = "Detached envelope owner joins; real medical service runtime/terminal ledger and controlled work/physical delivery; official physical/service/survival/consumables section restores; NOT a whole-world treatment restore",
                ["treatmentBridge"] = treatment,
                ["fuelBridge"] = fuel,
                ["fullAuditClosure"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false });
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject { ["passed"] = false, ["failure"] = error.ToString() });
            throw;
        }
    }

    public static void RunFacilityUseContinuationBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new create-only facility continuation output directory is required.");
        Directory.CreateDirectory(output);
        try
        {
            if (!FacilityUseExecutionStateDebugScenarios.RunAll(log: true))
                throw new InvalidOperationException("Facility progress state regression failed.");
            if (!ServiceSessionClockDebugScenarios.RunAll(log: true))
                throw new InvalidOperationException("Service clock regression failed.");
            MigratedProducerEconomyServiceEntryE2EDebugScenarios.RunFacilityUseContinuationScenarios();
            if (!ServiceSessionOutcomeDebugScenarios.RunAll(log: true))
                throw new InvalidOperationException("Service terminal payment regression failed.");
            WriteReport(output, new JObject {
                ["passed"] = true, ["test"] = "facility-use-continuation-and-service-terminal",
                ["scope"] = "Production Facility coroutine with controlled visitor/clock and module JSON restore; service terminal includes actual contracted/provided duration and retry-safe zero-income partial cancellation; not full-world PlayMode restore hook",
                ["e05PartialProvision"] = ServiceSessionOutcomeDebugScenarios.LastPartialProvisionEvidence
                    ?? throw new InvalidOperationException("E05 partial-provision export example is missing."),
                ["e05RawEvidence"] = ServiceSessionOutcomeDebugScenarios.LastRawEvidence
                    ?? throw new InvalidOperationException("E05 raw terminal evidence is missing."),
                ["fullAuditClosure"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false });
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject { ["passed"] = false, ["failure"] = error.ToString(),
                ["e05RawEvidence"] = ServiceSessionOutcomeDebugScenarios.LastRawEvidence });
            throw;
        }
    }

    public static void RunA01FacilityUseBatch()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "A new create-only A01 facility-use output directory is required.");
        }
        Directory.CreateDirectory(output);
        try
        {
            MigratedProducerEconomyServiceEntryE2EDebugScenarios
                .RunFacilityUseDetailedBatch();
            MigratedProducerEconomyServiceEntryE2EDebugScenarios
                .RunFacilityUseContinuationScenarios();
            if (!ServiceSessionOutcomeDebugScenarios.RunAll(log: true))
            {
                throw new InvalidOperationException(
                    "A01 service-session payment and partial-provision regression failed.");
            }
            WriteReport(output, new JObject
            {
                ["passed"] = true,
                ["test"] = "a01-facility-use",
                ["coverage"] = new JArray(
                    "actual-facility-interact-and-recovery",
                    "entry-failure-and-action-replacement-cancellation",
                    "five-role-need-before-after",
                    "partial-effects-after-payment-rejection",
                    "active-use-save-resume",
                    "parent-save-reload-retry-no-duplicate",
                    "service-duration-and-payment-terminal"),
                ["partialProvision"] =
                    ServiceSessionOutcomeDebugScenarios.LastPartialProvisionEvidence
                    ?? throw new InvalidOperationException(
                        "A01 partial-provision evidence is missing."),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject
            {
                ["passed"] = false,
                ["test"] = "a01-facility-use",
                ["failure"] = error.ToString(),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            throw;
        }
    }

    public static void RunA02PrimitiveSurvivalBatch()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "A new create-only A02 primitive-survival output directory is required.");
        }
        Directory.CreateDirectory(output);
        try
        {
            if (!PrimitiveSurvivalCausalProductionEntryDebugScenarios
                    .RunCausalOwnerFamily(log: true))
            {
                throw new InvalidOperationException(
                    "A02 causal primitive-survival owner returned false.");
            }
            PrimitiveSurvivalCausalProductionEntryDebugScenarios
                .RunB0RollbackBatch();
            WriteReport(output, new JObject
            {
                ["passed"] = true,
                ["test"] = "a02-primitive-survival",
                ["coverage"] = new JArray(
                    "floor-rest-child-rejection-no-false-success",
                    "floor-rest-reservation-rejection-no-mutation",
                    "floor-rest-body-partial-save-resume",
                    "latrine-reservation-rejection-no-mutation",
                    "latrine-body-filth-partial-save-resume",
                    "causal-rest-fresh-composition-restores",
                    "terminal-replay-no-op"),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject
            {
                ["passed"] = false,
                ["test"] = "a02-primitive-survival",
                ["failure"] = error.ToString(),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            throw;
        }
    }

    public static void RunA03VisitorExitBatch()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "A new create-only A03 visitor-exit output directory is required.");
        }
        Directory.CreateDirectory(output);
        string previous = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_A03_REPORT_PATH");
        try
        {
            Environment.SetEnvironmentVariable(
                "DUNGEONSTORY_A03_REPORT_PATH",
                Path.Combine(output, "a03-focused.txt"));
            if (!VisitorExitOutcomeDebugScenarios.RunAll(log: true))
            {
                throw new InvalidOperationException(
                    "A03 visitor-exit outcome scenarios returned false.");
            }
            WriteReport(output, new JObject
            {
                ["passed"] = true,
                ["test"] = "a03-visitor-exit-and-floor-theft",
                ["coverage"] = new JArray(
                    "exact-theft-target-stack-carry-destination",
                    "no-loose-item-no-mutation",
                    "outcome-rejection-full-rollback",
                    "theft-save-restore-departure-partial",
                    "blocked-failed-partial-departed-terminal-status",
                    "departure-owner-rollback-and-success",
                    "durable-terminal-replay-no-op",
                    "save-validation-tamper-rejection"),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject
            {
                ["passed"] = false,
                ["test"] = "a03-visitor-exit-and-floor-theft",
                ["failure"] = error.ToString(),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            throw;
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "DUNGEONSTORY_A03_REPORT_PATH",
                previous);
        }
    }

    public static void RunA04FacilityVandalismBatch()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "A new create-only A04 facility-vandalism output directory is required.");
        }
        Directory.CreateDirectory(output);
        string previous = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_A04_REPORT_PATH");
        try
        {
            string projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
            string focusedAbsolute = Path.GetFullPath(
                Path.Combine(output, "a04-focused.txt"));
            if (!focusedAbsolute.StartsWith(
                    projectRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "A04 focused report must remain inside the project.");
            }
            string focusedRelative = focusedAbsolute
                .Substring(projectRoot.Length + 1)
                .Replace('\\', '/');
            Environment.SetEnvironmentVariable(
                "DUNGEONSTORY_A04_REPORT_PATH",
                focusedRelative);
            if (!FacilityVandalismOutcomeDebugScenarios.RunAll(log: true))
            {
                throw new InvalidOperationException(
                    "A04 facility-vandalism outcome scenarios returned false.");
            }
            WriteReport(output, new JObject
            {
                ["passed"] = true,
                ["test"] = "a04-facility-vandalism",
                ["coverage"] = new JArray(
                    "exact-actor-facility-source-reason-child",
                    "commit-rejection-damage-owner-rollback",
                    "already-damaged-destroyed-no-prepare",
                    "macro-production-entry",
                    "breakdown-production-entry-movement-invalidation",
                    "aggregate-save-roundtrip-tamper-rejection-replay-no-op"),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject
            {
                ["passed"] = false,
                ["test"] = "a04-facility-vandalism",
                ["failure"] = error.ToString(),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            throw;
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "DUNGEONSTORY_A04_REPORT_PATH",
                previous);
        }
    }

    public static void RunWorkTerminalDetailBatch()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "A new create-only W07 terminal-detail output directory is required.");
        }
        Directory.CreateDirectory(output);
        try
        {
            JObject evidence = WorkTerminalDetailW07DebugScenarios.RunAll();
            evidence["test"] = "work-terminal-detail-w07";
            evidence["fullAuditClosure"] = false;
            evidence["humanApprovalClaimed"] = false;
            evidence["trainingEligible"] = false;
            WriteReport(output, evidence);
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject
            {
                ["passed"] = false,
                ["test"] = "work-terminal-detail-w07",
                ["failure"] = error.ToString(),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            throw;
        }
    }

    public static void RunConstructionDemolitionW02Batch()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "A new create-only W02 construction/demolition output directory is required.");
        }
        Directory.CreateDirectory(output);
        try
        {
            JObject work = WorkAmountDebugScenarios.RunW02Focused();
            if (!BuildingDemolitionOutcomeDebugScenarios.RunAll(
                    logSuccess: true))
            {
                throw new InvalidOperationException(
                    "W02 demolition outcome scenarios failed.");
            }
            var branches = (JArray)work["branches"];
            branches.Add(new JObject
            {
                ["id"] = "world-removal-before-ledger-result",
                ["passed"] = true,
                ["assertion"] = "the canonical demolition ledger contains zero results before removal and exactly one after removal"
            });
            branches.Add(new JObject
            {
                ["id"] = "post-removal-ledger-rejection-retry",
                ["passed"] = true,
                ["assertion"] = "an injected post-removal ledger rejection remains uncommitted and retries to exactly one canonical result"
            });
            work["test"] = "construction-demolition-w02";
            work["scope"] = "real work-order material, contribution, placement, "
                + "save serialization, destructive-drain, world removal, salvage, "
                + "and gameplay-outcome owners with controlled authority faults";
            work["fullAuditClosure"] = false;
            work["humanApprovalClaimed"] = false;
            work["trainingEligible"] = false;
            WriteReport(output, work);
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject
            {
                ["passed"] = false,
                ["test"] = "construction-demolition-w02",
                ["failure"] = error.ToString(),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            throw;
        }
    }

    public static void RunWildlifeFoodRaidE04Batch()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "A new create-only E04 wildlife food-raid output directory is required.");
        }
        Directory.CreateDirectory(output);
        try
        {
            JObject evidence = WildlifeFoodRaidOutboxDebugScenarios
                .RunE04Focused();
            evidence["test"] = "wildlife-food-raid-e04";
            evidence["scope"] = "public wildlife raid behavior, physical item "
                + "consumption, causal gameplay outcomes, cancellation after "
                + "consumption, pending-owner recovery, and pre-theft failures";
            evidence["fullAuditClosure"] = false;
            evidence["humanApprovalClaimed"] = false;
            evidence["trainingEligible"] = false;
            WriteReport(output, evidence);
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject
            {
                ["passed"] = false,
                ["test"] = "wildlife-food-raid-e04",
                ["failure"] = error.ToString(),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            throw;
        }
    }

    public static void RunServiceSessionOutcomeBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new create-only service-session regression output directory is required.");
        Directory.CreateDirectory(output);
        try
        {
            if (!ServiceSessionOutcomeDebugScenarios.RunAll(log: true))
                throw new InvalidOperationException("Service-session terminal regression failed.");
            WriteReport(output, new JObject {
                ["passed"] = true,
                ["test"] = "service-session-terminal-outcomes-with-payment-faults",
                ["scope"] = "Production service-session terminal owner and actual GameMoneyAccount/EconomyTransactionLedgerRuntime income join, canonical fresh finance/session owner restores and original controlled payment faults; not Brain, live active-service continuation, whole-world or cold-process restore",
                ["e05PartialProvision"] = ServiceSessionOutcomeDebugScenarios.LastPartialProvisionEvidence
                    ?? throw new InvalidOperationException("E05 partial-provision export example is missing."),
                ["e05RawEvidence"] = ServiceSessionOutcomeDebugScenarios.LastRawEvidence
                    ?? throw new InvalidOperationException("E05 raw terminal evidence is missing."),
                ["fullAuditClosure"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false });
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject { ["passed"] = false, ["failure"] = error.ToString(),
                ["e05RawEvidence"] = ServiceSessionOutcomeDebugScenarios.LastRawEvidence });
            throw;
        }
    }

    public static void RunFacilityContextBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new create-only facility regression output directory is required.");
        Directory.CreateDirectory(output);
        try
        {
            if (!FacilityEvolutionDebugScenarios.RunAll(log: true))
                throw new InvalidOperationException("Facility evolution regression failed.");
            WriteReport(output, new JObject { ["passed"] = true, ["test"] = "facility-evolution-with-exact-nested-json",
                ["fullAuditClosure"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false });
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject { ["passed"] = false, ["failure"] = error.ToString() });
            throw;
        }
    }

    public static void RunLedgerTemporalBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new create-only ledger temporal output directory is required.");
        Directory.CreateDirectory(output);
        try
        {
            string result = GameplayOutcomeLedgerDebugScenarios.RunAll();
            WriteReport(output, new JObject { ["passed"] = true, ["test"] = result,
                ["fullAuditClosure"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false });
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject { ["passed"] = false, ["failure"] = error.ToString() });
            throw;
        }
    }

    public static void RunDelayedOutboxBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new create-only outbox output directory is required.");
        Directory.CreateDirectory(output);
        try
        {
            GameplayOutcomeLedgerDebugScenarios.VerifyDelayedPublicationTime();
            WriteReport(output, new JObject { ["passed"] = true, ["test"] = "delayed-publication-time",
                ["fullAuditClosure"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false });
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject { ["passed"] = false, ["failure"] = error.ToString() });
            throw;
        }
    }

    public static void RunRestorePrerequisiteBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new create-only prerequisite output directory is required.");
        Directory.CreateDirectory(output);
        string previous = Environment.GetEnvironmentVariable("DUNGEONSTORY_A05_REPORT_PATH");
        try
        {
            Environment.SetEnvironmentVariable("DUNGEONSTORY_A05_REPORT_PATH", Path.Combine(output, "a05.txt"));
            BuildingDemolitionOutcomeDebugScenarios.RunOrThrow();
            if (!CharacterSkillActivationOutcomeDebugScenarios.RunAll(log: true))
                throw new InvalidOperationException("Skill activation prerequisites failed.");
            WriteReport(output, new JObject { ["passed"] = true,
                ["tests"] = new JArray("demolition-with-checkpoint-gc", "skill-activation-with-owner-fixed"),
                ["fullAuditClosure"] = false });
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject { ["passed"] = false, ["failure"] = error.ToString() });
            throw;
        }
        finally { Environment.SetEnvironmentVariable("DUNGEONSTORY_A05_REPORT_PATH", previous); }
    }

    public static void RunMigratedProducerRegressionBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new diagnostic output directory is required.");
        Directory.CreateDirectory(output);
        try
        {
            MigratedProducerOutcomeDebugScenarios.RunAll(throwOnFailure: true);
            WriteReport(output, new JObject { ["passed"] = true, ["test"] = "migrated-producer-all-72" });
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject { ["passed"] = false, ["test"] = "migrated-producer-all-72",
                ["failure"] = error.ToString() });
            throw;
        }
    }

    public static void RunIntegrationBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new integration output directory is required.");
        Directory.CreateDirectory(output);
        string originalPhase80 = Environment.GetEnvironmentVariable("DUNGEONSTORY_PHASE80_FOCUSED_REPORT_PATH");
        string originalA05 = Environment.GetEnvironmentVariable("DUNGEONSTORY_A05_REPORT_PATH");
        var results = new JArray();
        void Run(string name, Action test)
        {
            Environment.SetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT", Path.Combine(output, name));
            try { test(); results.Add(new JObject { ["test"] = name, ["passed"] = true }); }
            catch (Exception error) { results.Add(new JObject { ["test"] = name, ["passed"] = false, ["failure"] = error.ToString() }); }
        }
        try
        {
            Environment.SetEnvironmentVariable("DUNGEONSTORY_A05_REPORT_PATH", Path.Combine(output, "a05-focused.txt"));
            Run("calendar", RunBodyHealthCalendarRegressionBatch);
            Run("eligibility", GameplayModuleOwnerEligibilityDebugScenarios.RunBatch);
            Run("lifecycle-revision", CharacterLifecycleRevisionDebugScenarios.RunBatch);
            Run("d13-public", MigratedProducerCombatDefenseEntryE2EDebugScenarios.RunD13PublicCellFireBatch);
            string project = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            Environment.SetEnvironmentVariable("DUNGEONSTORY_PHASE80_FOCUSED_REPORT_PATH",
                Path.GetRelativePath(project, Path.Combine(output, "phase80", "report.json")).Replace('\\', '/'));
            Run("phase80", GameplayOutcomeLedgerPhase80BatchRunner.RunBatch);
            Run("character-progression", () =>
            {
                if (!CharacterProgressionDebugScenarios.RunAll(logSuccess: true))
                    throw new InvalidOperationException("Character progression regression returned false.");
            });
            Run("equipment-presentation-context", EquipmentItemStateV18DebugScenarios.RunAll);
            Run("random-stream-isolation", () => RandomStreamIsolationDebugScenarios.RunAll());
            Run("offense-world-restore-isolation", OffenseStrategicDebugScenarios.VerifyRestoreTransactionIsolation);
            Run("restore-publication-lifecycle", OffenseRestorePublicationDebugScenarios.VerifyPublicationLifecycle);
            Run("offense-runtime-owner-isolation", OffenseRewardDebugScenarios.VerifyRuntimeOwnerRestoreIsolation);
            Run("offense-strategic-owner-isolation", OffenseStrategicDebugScenarios.VerifyStrategicOwnerRestoreIsolation);
            Run("acknowledged-provenance-preflight", ProductionAcknowledgedOutputLifecycleSaveValidationScenarios.RunAll);
            Run("planned-output-publication", FacilityBufferPlannedOutputPublicationDebugScenarios.RunAll);
            Run("physical-exact-source-publication", PhysicalItemExactSourcePublicationDebugScenarios.RunAll);
            Run("exact-capability-restore-join", ProductionExactCapabilityOutputRestoreJoinDebugScenarios.RunAll);
            Run("provenance-lifecycle", PlannedOutputProvenanceLifecycleDebugScenarios.RunAll);
            Run("haul-telemetry-noninterference", RestoredCarrySubsetReleaseDebugScenarios.RunAll);
            Run("haul-construction-destination", () =>
            {
                bool haulPassed = HaulPlanConstructionSafetyDebugScenarios.RunAll(logSuccess: true);
                File.Copy("Temp/haul-plan-construction-safety.tsv",
                    Path.Combine(output, "haul-construction-destination.tsv"), overwrite: false);
                if (!haulPassed)
                    throw new InvalidOperationException("Haul/construction destination regressions failed.");
            });
            Run("output-clearance-diagnostics", FacilityOutputClearanceTelemetryDebugScenarios.RunAll);
            Run("population-preview", () =>
            {
                if (!CharacterPopulationDebugScenarios.RunAll(logSuccess: true))
                    throw new InvalidOperationException("Population preview regressions failed.");
            });
            Run("unfinished-fixture-guard", () =>
            {
                if (!RoomProfileConstructionGuardDebugScenarios.RunAll())
                    throw new InvalidOperationException("Unfinished fixture regressions failed.");
            });
            bool passed = results.Cast<JObject>().All(value => (bool)value["passed"]);
            WriteReport(output, new JObject { ["passed"] = passed, ["results"] = results,
                ["fullAuditClosure"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false });
            if (!passed) throw new InvalidOperationException("Integrated audit regressions failed; inspect report.json.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT", output);
            Environment.SetEnvironmentVariable("DUNGEONSTORY_PHASE80_FOCUSED_REPORT_PATH", originalPhase80);
            Environment.SetEnvironmentVariable("DUNGEONSTORY_A05_REPORT_PATH", originalA05);
        }
    }

    public static void RunBodyHealthCalendarRegressionBatch()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new create-only audit output directory is required.");
        Directory.CreateDirectory(output);
        IGameCalendar calendar = CharacterAiEditorTestDependencies.GameCalendar;
        int originalDay = calendar.Day;
        int originalHour = calendar.Hour;
        var stages = new JArray();
        JObject checkpoint = null;
        const string ownerId = "combat-life-cycle:audit-calendar:owner";
        const string characterId = "character:audit-calendar:protagonist";
        string[] actions =
        {
            MigratedProducerCombatDefenseEntryE2EDebugScenarios.NonlethalCombatInjuryActionId,
            MigratedProducerCombatDefenseEntryE2EDebugScenarios.RecoverBodyHealthActionId,
            MigratedProducerCombatDefenseEntryE2EDebugScenarios.FatalCombatInjuryActionId
        };
        try
        {
            for (int index = 0; index < actions.Length; index++)
            {
                int day = 137 + index;
                calendar.SetDateTime(day, 9);
                IReadOnlyList<IProductionCausalActionFixture> fixtures =
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios.CreateCausalCombatLifeCycleFixtures(
                        "run:audit-calendar:" + index, characterId, ownerId, checkpoint);
                try
                {
                    IProductionCausalActionFixture action = fixtures.Single(value => value.ActionId == actions[index]);
                    IGameCalendar restoredCalendar = MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .RequireLifeCycleCalendar(action);
                    if (index > 0 && (restoredCalendar.Day != day - 1 || restoredCalendar.Hour != 9))
                        throw new InvalidOperationException("The saved game calendar did not restore with its owner.");
                    restoredCalendar.SetDateTime(day, 9);
                    int previousCount = checkpoint == null ? 0 : Exact(checkpoint).Count;
                    if (!action.Execute())
                        throw new InvalidOperationException("Expected real life-cycle command to commit: " + actions[index]);
                    checkpoint = ((IProductionCausalOwnerCheckpointFixture)action).CaptureOwnerCheckpoint();
                    JArray outcomes = Exact(checkpoint);
                    JObject[] additions = outcomes.Skip(previousCount).OfType<JObject>().ToArray();
                    if (additions.Length == 0 || additions.Any(value => (int?)value["absoluteDay"] != day))
                        throw new InvalidOperationException("Health/combat events did not use the authoritative calendar day.");
                    int[] days = outcomes.OfType<JObject>().Select(value => (int)value["absoluteDay"]).ToArray();
                    if (days.Zip(days.Skip(1), (left, right) => left > right).Any(value => value))
                        throw new InvalidOperationException("Synchronous life-cycle outcome dates went backwards.");
                    string file = "checkpoint-stage-" + (index + 1) + ".json";
                    File.WriteAllText(Path.Combine(output, file), checkpoint.ToString(Formatting.None));
                    stages.Add(new JObject
                    {
                        ["actionId"] = actions[index], ["absoluteDay"] = day,
                        ["newExactOutcomeCount"] = additions.Length,
                        ["exactOutcomeCount"] = outcomes.Count, ["checkpointFile"] = file,
                        ["restoredFromPreviousCheckpoint"] = index > 0,
                        ["savedCalendarRestored"] = index > 0,
                        ["passed"] = true
                    });
                }
                finally
                {
                    foreach (IProductionCausalActionFixture fixture in fixtures)
                        fixture.Dispose();
                }
            }
            // Restore the terminal aggregate and repeat the last command: no event or state may be added.
            IReadOnlyList<IProductionCausalActionFixture> terminal =
                MigratedProducerCombatDefenseEntryE2EDebugScenarios.CreateCausalCombatLifeCycleFixtures(
                    "run:audit-calendar:terminal", characterId, ownerId, checkpoint);
            try
            {
                IProductionCausalActionFixture action = terminal.Single(value => value.ActionId == actions[2]);
                JObject before = ((IProductionCausalOwnerCheckpointFixture)action).CaptureOwnerCheckpoint();
                if (action.Execute() || !JToken.DeepEquals(before,
                        ((IProductionCausalOwnerCheckpointFixture)action).CaptureOwnerCheckpoint()))
                    throw new InvalidOperationException("Terminal replay changed state or added events.");
            }
            finally
            {
                foreach (IProductionCausalActionFixture fixture in terminal)
                    fixture.Dispose();
            }
            JObject earlierCalendar = (JObject)checkpoint.DeepClone();
            earlierCalendar["domainState"]["session"]["absoluteDay"] = 136;
            RequireRestoreRejected(earlierCalendar, ownerId, characterId, "FUTURE_OUTCOME");
            JObject inconsistentCalendar = (JObject)checkpoint.DeepClone();
            inconsistentCalendar["domainState"]["session"]["hour"] = 3;
            RequireRestoreRejected(inconsistentCalendar, ownerId, characterId, "INCONSISTENT_GAME_TIME");
            WriteReport(output, new JObject
            {
                ["schemaVersion"] = 1, ["testId"] = "audit-u2-body-health-calendar",
                ["passed"] = true, ["stages"] = stages, ["terminalReplayUnchanged"] = true,
                ["futureFactRejected"] = true, ["inconsistentSavedTimeRejected"] = true,
                ["controlledDomainExecution"] = true, ["naturalPlayTelemetry"] = false,
                ["fullU2Closure"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
            });
        }
        catch (Exception exception)
        {
            WriteReport(output, new JObject
            {
                ["schemaVersion"] = 1, ["testId"] = "audit-u2-body-health-calendar",
                ["passed"] = false, ["stages"] = stages, ["failure"] = exception.ToString(),
                ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
            });
            throw;
        }
        finally
        {
            calendar.SetDateTime(originalDay, originalHour);
        }
    }

    private static JArray Exact(JObject checkpoint) =>
        checkpoint?["outcomeLedger"]?["exactOutcomes"] as JArray
        ?? throw new InvalidOperationException("Actual owner checkpoint lacks its exact outcome ledger.");

    private static void RequireRestoreRejected(JObject checkpoint, string ownerId, string characterId, string reason)
    {
        try
        {
            var fixtures = MigratedProducerCombatDefenseEntryE2EDebugScenarios.CreateCausalCombatLifeCycleFixtures(
                "run:audit-calendar:invalid", characterId, ownerId, checkpoint);
            foreach (var fixture in fixtures) fixture.Dispose();
        }
        catch (InvalidOperationException error) when (error.Message.Contains(reason)) { return; }
        throw new InvalidOperationException("Invalid saved calendar was accepted: " + reason);
    }

    private static void WriteReport(string output, JObject report)
    {
        using var stream = new FileStream(Path.Combine(output, "report.json"), FileMode.CreateNew);
        using var writer = new StreamWriter(stream);
        writer.Write(report.ToString(Formatting.Indented));
    }
}
#endif
