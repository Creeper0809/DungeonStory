#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsProcessStartInfo = System.Diagnostics.ProcessStartInfo;

public static partial class ProductionCausalPlanBatchExporter
{
    private const string CatalogSchema =
        "dungeonstory-causal-gameplay-action-catalog@4";
    private const string AtomicCatalogSchema =
        "dungeonstory-causal-gameplay-action-catalog@3";
    private const string PreviousCatalogSchema =
        "dungeonstory-causal-gameplay-action-catalog@2";
    private const string LegacyCatalogSchema =
        "dungeonstory-causal-gameplay-action-catalog@1";
    private const string PlanSchema = "dungeonstory-causal-gameplay-plan@2";
    private const string LegacyPlanSchema = "dungeonstory-causal-gameplay-plan@1";
    private const string ResultSchema = "dungeonstory-causal-gameplay-result@5";
    private const string AtomicResultSchema = "dungeonstory-causal-gameplay-result@4";
    private const string CatalogVersion = "causal-actions-v4";
    private const string CatalogParent =
        "Artifacts/Exports/CausalGameplayActionCatalog";
    private const string ResultParent =
        "Artifacts/Exports/CausalGameplayExecution";

    public static void RunCatalogBatchFromEnvironment()
    {
        string version = RequireVersion(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_CATALOG_VERSION"));
        string path = ExportCatalog(version);
        Debug.Log("CAUSAL_GAMEPLAY_ACTION_CATALOG=PASS; path=" + path);
    }

    public static void RunPlanBatchFromEnvironment()
    {
        string version = RequireVersion(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_EXECUTION_VERSION"));
        string planPath = RequireExistingPath(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_PLAN_PATH"), "plan");
        string catalogPath = RequireExistingPath(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_CATALOG_PATH"), "catalog");
        string path = ExecutePlan(version, planPath, catalogPath);
        Debug.Log("CAUSAL_GAMEPLAY_EXECUTION=PASS; path=" + path);
    }

    public static void RunLiveCatalogBatchFromEnvironment()
    {
        string version = RequireVersion(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_CATALOG_VERSION"));
        Debug.Log("LIVE_CAUSAL_ACTION_CATALOG=PASS; path=" + ExportCatalog(version, live: true));
    }

    internal static CausalGameplayPlan LoadValidatedPlan(string planPath, string catalogPath, out JObject catalog)
    {
        string commit = ReadGit(ProjectRoot(), "rev-parse HEAD");
        catalog = StrictLoad(catalogPath);
        ValidateCatalog(catalog, commit);
        return ParsePlan(StrictLoad(planPath), catalog, "sha256:" + Sha256File(catalogPath), commit);
    }

    public static string ExportCatalog(string version, bool live = false)
    {
        string canonicalVersion = RequireVersion(version);
        string projectRoot = ProjectRoot();
        string destination = PrepareCreateOnlyDestination(
            projectRoot,
            CatalogParent,
            canonicalVersion);
        string staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        try
        {
            string commit = ReadGit(projectRoot, "rev-parse HEAD");
            JObject catalog = live
                ? LiveNarrativeCausalPlan.BuildCatalog(canonicalVersion, commit)
                : BuildCatalog(canonicalVersion, commit);
            string catalogPath = Path.Combine(staging, "action_catalog.json");
            WriteJson(catalogPath, catalog);
            JObject manifest = new()
            {
                ["schemaVersion"] = 4,
                ["schema"] = CatalogSchema,
                ["version"] = canonicalVersion,
                ["catalogVersion"] = CatalogVersion,
                ["gameCommit"] = (string)catalog["gameCommit"],
                ["workingTreeDirty"] = ReadGit(
                    projectRoot,
                    "status --porcelain").Length > 0,
                ["authority"] = "unity-csharp-authoritative-action-catalog",
                ["actionCount"] = ((JArray)catalog["actions"]).Count,
                ["domainCount"] = ((JArray)catalog["actions"])
                    .Select(value => (string)value["domainId"])
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false,
                ["catalogSha256"] = Sha256File(catalogPath),
                ["sourceFiles"] = new JArray(SourceFiles(projectRoot))
            };
            WriteJson(Path.Combine(staging, "manifest.json"), manifest);
            WriteDelivery(staging, "action_catalog.json", "manifest.json");
            Directory.Move(staging, destination);
            return destination;
        }
        catch
        {
            // Preserve the unique staging directory as failed-attempt evidence.
            // Only the atomic move above publishes a completed export.
            throw;
        }
    }

    public static string ExecutePlan(
        string version,
        string planPath,
        string catalogPath)
    {
        string canonicalVersion = RequireVersion(version);
        string projectRoot = ProjectRoot();
        string destination = PrepareCreateOnlyDestination(
            projectRoot,
            ResultParent,
            canonicalVersion);
        string staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        try
        {
            string gameCommit = ReadGit(projectRoot, "rev-parse HEAD");
            JObject catalog = StrictLoad(catalogPath);
            ValidateCatalog(catalog, gameCommit);
            string catalogSha256 = "sha256:" + Sha256File(catalogPath);
            JObject planJson = StrictLoad(planPath);
            CausalGameplayPlan plan = ParsePlan(
                planJson,
                catalog,
                catalogSha256,
                gameCommit);

            CausalGameplayExecutionResult execution;
            using (var world = new ProductionPlanWorld(plan))
            {
                CausalGameplayActionRegistry registry = CreateRegistry(world);
                ValidatePlanActionsMatchCatalog(plan, catalog, registry);
                execution = new CausalGameplayPlanExecutor(registry, world)
                    .Execute(plan);
            }

            JObject result = ResultJson(plan, execution, catalogSha256);
            string resultPath = Path.Combine(staging, "execution_result.json");
            WriteJson(resultPath, result);
            string replanFile = string.Empty;
            if (execution.ReplanRequired)
            {
                replanFile = "replan_request.json";
                WriteJson(
                    Path.Combine(staging, replanFile),
                    ReplanRequest(plan, execution, catalogSha256, gameCommit));
            }

            var files = new List<string>
            {
                "input_plan.json",
                "input_action_catalog.json",
                "execution_result.json"
            };
            File.Copy(planPath, Path.Combine(staging, "input_plan.json"));
            File.Copy(catalogPath, Path.Combine(staging, "input_action_catalog.json"));
            if (replanFile.Length > 0)
                files.Add(replanFile);

            JObject manifest = new()
            {
                ["schemaVersion"] = ResultVersionForPlan(plan),
                ["schema"] = ResultSchemaForPlan(plan),
                ["version"] = canonicalVersion,
                ["gameCommit"] = gameCommit,
                ["workingTreeDirty"] = ReadGit(
                    projectRoot,
                    "status --porcelain").Length > 0,
                ["catalogVersion"] = plan.CatalogVersion,
                ["catalogSha256"] = catalogSha256,
                ["planId"] = plan.PlanId,
                ["planRevision"] = plan.Revision,
                ["planSha256"] = plan.PlanSha256,
                ["status"] = execution.ReplanRequired
                    ? "replan-required"
                    : "completed",
                ["actualGameplayProductionEntries"] = true,
                ["naturalPlayTelemetry"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false,
                ["executedCount"] = execution.Records.Count(value =>
                    !string.IsNullOrEmpty(value.OutcomeId)),
                ["invalidatedCount"] = execution.InvalidatedNodeIds.Count,
                ["resultSha256"] = Sha256File(resultPath),
                ["sourceFiles"] = new JArray(SourceFiles(projectRoot))
            };
            WriteJson(Path.Combine(staging, "manifest.json"), manifest);
            files.Add("manifest.json");
            WriteDelivery(staging, files.ToArray());
            Directory.Move(staging, destination);
            return destination;
        }
        catch
        {
            // Keep partial execution records for diagnosis; never publish them as complete.
            throw;
        }
    }

    private static JObject BuildCatalog(string version, string gameCommit) => new()
    {
        ["schemaVersion"] = 4,
        ["schema"] = CatalogSchema,
        ["kind"] = "unity-causal-gameplay-action-catalog",
        ["version"] = version,
        ["catalogVersion"] = CatalogVersion,
        ["gameCommit"] = gameCommit,
        ["authority"] = "unity-csharp-authoritative-action-catalog",
        ["naturalPlayClaimed"] = false,
        ["humanApprovalClaimed"] = false,
        ["trainingEligible"] = false,
        ["subjectRoles"] = new JArray
        {
            SubjectRole("role:protagonist", "character", "plan-derived", "human-npc"),
            SubjectRole("role:combat-enemy", "character", "plan-derived", "human-npc"),
            SubjectRole("role:combat-attacker", "character", "fixture-owned", "human-npc"),
            SubjectRole("role:combat-victim", "character", "fixture-owned", "human-npc"),
            SubjectRole("role:stock-supply-lumber", "item", "fixture-owned", "physical-world-item-stack"),
            SubjectRole("role:stock-transform-output", "item", "fixture-owned", "authored-stackable-item"),
            SubjectRole("role:facility-procurement-policy", "policy", "fixture-owned", "auto-procurement-policy"),
            SubjectRole("role:facility-procurement-target", "facility", "fixture-owned", "authored-building"),
            SubjectRole("role:career-mentor", "character", "fixture-owned", "mentor-npc"),
            SubjectRole("role:career-student", "character", "fixture-owned", "student-npc"),
            SubjectRole("role:defense-facility", "facility", "fixture-owned", "authored-building"),
            SubjectRole("role:defense-target", "character", "fixture-owned", "human-intruder"),
            SubjectRole("role:faction", "faction", "fixture-owned", "faction"),
            SubjectRole("role:offense-ally", "character", "fixture-owned", "battle-ally"),
            SubjectRole("role:offense-enemy", "character", "fixture-owned", "battle-enemy"),
            SubjectRole("role:treatment-subject", "character", "fixture-owned", "consumables-actor"),
            SubjectRole("role:retail-customer", "character", "fixture-owned", "retail-customer"),
            SubjectRole("role:retail-facility", "facility", "fixture-owned", "retail-building"),
            SubjectRole("role:crop-plot", "facility", "fixture-owned", "authored-crop-plot"),
            SubjectRole("role:irrigation-facility", "facility", "fixture-owned", "authored-irrigator"),
            SubjectRole("role:fire-target", "facility", "fixture-owned", "combustible-building"),
            SubjectRole("role:fire-responder", "character", "fixture-owned", "fire-response-worker"),
            SubjectRole("role:room-worker", "character", "fixture-owned", "human-npc"),
            SubjectRole("role:room-facility", "facility", "fixture-owned", "authored-room-fixture"),
            SubjectRole("role:room", "room", "fixture-owned", "detected-room"),
            SubjectRole("role:species-character", "character", "fixture-owned", "golem-npc"),
            SubjectRole("role:species-definition", "species-definition", "fixture-owned", "authored-species"),
            SubjectRole("role:certified-seed-facility", "facility", "fixture-owned", "authored-certification-workstation"),
            SubjectRole("role:certified-seed-output", "item", "fixture-owned", "authored-seed-item"),
            SubjectRole("role:social-offender", "character", "fixture-owned", "human-npc"),
            SubjectRole("role:social-recipient", "character", "fixture-owned", "human-npc"),
            SubjectRole("role:staff-rebel", "character", "fixture-owned", "human-staff"),
            SubjectRole("role:disruptive-worker", "character", "fixture-owned", "human-staff"),
            SubjectRole("role:staff-responder", "character", "fixture-owned", "human-manager"),
            SubjectRole("role:emergency-first-worker", "character", "fixture-owned", "human-staff"),
            SubjectRole("role:emergency-second-worker", "character", "fixture-owned", "human-staff"),
            SubjectRole("role:emergency-work-facility", "facility", "fixture-owned", "authored-building"),
            SubjectRole("role:first-boss-award-region", "region", "fixture-owned", "authored-offense-region"),
            SubjectRole("role:second-boss-award-region", "region", "fixture-owned", "authored-offense-region"),
            SubjectRole("role:boss-award-seal", "item", "fixture-owned", "authored-item"),
            SubjectRole("role:trade-inventory-source", "item", "fixture-owned", "physical-world-item-stack"),
            SubjectRole("role:trade-inventory-destination", "item", "fixture-owned", "physical-world-item-stack"),
            SubjectRole("role:trade-inventory-waste-policy", "policy", "fixture-owned", "authored-waste-policy"),
            SubjectRole("role:disease-route-actor", "character", "fixture-owned", "human-npc"),
            SubjectRole("role:disease-route-vector", "wildlife", "fixture-owned", "wildlife-disease-vector"),
            SubjectRole("role:disease-route-disease", "disease-definition", "fixture-owned", "authored-disease"),
            SubjectRole("role:offense-node-expedition", "expedition", "fixture-owned", "offense-expedition"),
            SubjectRole("role:expedition", "expedition", "fixture-owned", "offense-expedition"),
            SubjectRole("role:special-wildlife-reward", "operation", "fixture-owned", "authored-special-wildlife-reward"),
            SubjectRole("role:arrival-wildlife", "wildlife", "fixture-owned", "physical-arrival-wildlife"),
            SubjectRole("role:festival-occurrence", "operation", "fixture-owned", "authored-festival-occurrence"),
            SubjectRole("role:festival-participant", "character", "fixture-owned", "festival-attendee"),
            SubjectRole("role:memorial-venue", "facility", "fixture-owned", "authored-memorial-venue"),
            SubjectRole("role:offense-truth-preliminary-expedition", "expedition", "fixture-owned", "offense-expedition"),
            SubjectRole("role:offense-truth-final-target", "expedition-target", "fixture-owned", "authored-offense-target"),
            SubjectRole("role:work-identity-worker", "character", "fixture-owned", "human-npc"),
            SubjectRole("role:run-operation-variable", "operation", "fixture-owned", "run-variable"),
            SubjectRole("role:owner-run-character", "character", "fixture-owned", "authored-owner"),
            SubjectRole("role:entered-visitor", "character", "fixture-owned", "already-entered-reception-visitor"),
            SubjectRole("role:visitor-checkout-facility", "facility", "fixture-owned", "staffed-retail-counter"),
            SubjectRole("role:taboo-butcher", "character", "fixture-owned", "authored-dungeon-owner"),
            SubjectRole("role:taboo-victim", "character", "fixture-owned", "authored-transient-customer"),
            SubjectRole("role:taboo-witness", "character", "fixture-owned", "authored-staff-witness"),
            SubjectRole("role:taboo-corpse", "item", "fixture-owned", "physical-humanoid-corpse-stack"),
            SubjectRole("role:owner-run-operation", "operation", "fixture-owned", "owner-run-settlement-and-upgrade"),
            SubjectRole("role:meta-run-progression", "operation", "fixture-owned", "meta-progression"),
            SubjectRole("role:regular-customer", "character", "fixture-owned", "customer"),
            SubjectRole("role:character-life", "character", "fixture-owned", "human-npc"),
            SubjectRole("role:service-hub", "facility", "fixture-owned", "authored-service-hub"),
            SubjectRole("role:service-visitor", "character", "fixture-owned", "customer"),
            SubjectRole("role:meal-incident-customer", "character", "fixture-owned", "customer"),
            SubjectRole("role:service-day-operation", "operation", "fixture-owned", "operating-day"),
            SubjectRole("role:invasion-intruder", "character", "fixture-owned", "authored-invasion-intruder"),
            SubjectRole("role:invasion-owner", "character", "fixture-owned", "dungeon-owner"),
            SubjectRole("role:invasion-damage-facility", "facility", "fixture-owned", "authored-defense-facility"),
            SubjectRole("role:defense-front", "operation", "fixture-owned", "defense-front"),
            SubjectRole("role:trait-character", "character", "fixture-owned", "human-npc"),
            SubjectRole("role:memory-erasure-seal", "item", "fixture-owned", "authored-item"),
            SubjectRole("role:interaction-captive", "character", "fixture-owned", "human-captive"),
            SubjectRole("role:interaction-warden", "character", "fixture-owned", "human-warden"),
            SubjectRole("role:interaction-facility", "facility", "fixture-owned", "authored-captive-housing"),
            SubjectRole("role:captive-performer", "character", "fixture-owned", "human-captive"),
            SubjectRole("role:ransomed-captive", "character", "fixture-owned", "human-captive"),
            SubjectRole("role:escaped-captive", "character", "fixture-owned", "human-captive"),
            SubjectRole("role:apparel-maker", "character", "fixture-owned", "human-npc"),
            SubjectRole("role:apparel-wearer", "character", "fixture-owned", "human-npc"),
            SubjectRole("role:apparel-facility", "facility", "fixture-owned", "authored-building"),
            SubjectRole("role:apparel-item", "item", "fixture-owned", "authored-apparel"),
            SubjectRole("role:research-task", "knowledge-task", "fixture-owned", "knowledge-residue"),
            SubjectRole("role:research-facility", "facility", "fixture-owned", "authored-research-building"),
            SubjectRole("role:production-facility", "facility", "fixture-owned", "authored-production-building"),
            SubjectRole("role:production-worker", "character", "fixture-owned", "human-npc"),
            SubjectRole("role:facility-evolution-target", "facility", "fixture-owned", "authored-evolving-building"),
            SubjectRole("role:facility-synthesis-target", "facility", "fixture-owned", "authored-synthesis-lineage"),
            SubjectRole("role:facility-synthesis-material", "facility", "fixture-owned", "authored-synthesis-material"),
            SubjectRole("role:infrastructure-facility", "facility", "fixture-owned", "authored-power-connected-building"),
            SubjectRole("role:dungeon-space", "dungeon-space", "fixture-owned", "main-dungeon-interior"),
            SubjectRole("role:unlocking-research", "research-project", "fixture-owned", "authored-research-project")
        },
        ["stateKeys"] = new JArray
        {
            StateKey("actor.id", "string"),
            StateKey("actor.thirst", "number"),
            StateKey("actor.sleep", "number"),
            StateKey("actor.proficiencyMilliExperience", "number"),
            StateKey("world.cleanWaterRemaining", "number"),
            StateKey("combat.ownerSnapshot", "string"),
            StateKey("facility.ownerSnapshot", "string"),
            StateKey("relationship.ownerSnapshot", "string"),
            StateKey("offense.ownerSnapshot", "string"),
            StateKey("treatment.ownerSnapshot", "string"),
            StateKey("health.actorId", "string"),
            StateKey("health.ownerId", "string"),
            StateKey("health.attackerId", "string"),
            StateKey("health.ownerSnapshot", "string"),
            StateKey("health.stage", "number"),
            StateKey("health.currentHealth", "number"),
            StateKey("health.downed", "bool"),
            StateKey("health.attackOutcomeCount", "number"),
            StateKey("health.thresholdOutcomeCount", "number"),
            StateKey("health.downedOutcomeCount", "number"),
            StateKey("health.recoveredOutcomeCount", "number"),
            StateKey("health.deathOutcomeCount", "number"),
            StateKey("health.killedOutcomeCount", "number"),
            StateKey("health.exactOutcomeCount", "number"),
            StateKey("economy.stockCount", "number"),
            StateKey("economy.actorMoney", "number"),
            StateKey("economy.purchaseCount", "number"),
            StateKey("crop.plotId", "string"),
            StateKey("crop.phase", "string"),
            StateKey("crop.currentWater", "number"),
            StateKey("crop.growthProgress", "number"),
            StateKey("crop.sourceWaterRemaining", "number"),
            StateKey("crop.harvestItemCount", "number"),
            StateKey("fire.ownerId", "string"),
            StateKey("fire.phase", "string"),
            StateKey("fire.fireId", "string"),
            StateKey("fire.intensity", "number"),
            StateKey("fire.remainingFuel", "number"),
            StateKey("fire.totalDamage", "number"),
            StateKey("fire.totalWaterConsumed", "number"),
            StateKey("fire.targetHitPoints", "number"),
            StateKey("fire.fuelStackQuantity", "number"),
            StateKey("fire.waterStackQuantity", "number"),
            StateKey("fire.plainIgnited", "bool"),
            StateKey("fire.fuelIgnited", "bool"),
            StateKey("fire.initialAttackApplied", "bool"),
            StateKey("fire.damageApplied", "bool"),
            StateKey("fire.waterSuppressed", "bool"),
            StateKey("social.ownerId", "string"),
            StateKey("social.offenderId", "string"),
            StateKey("social.recipientId", "string"),
            StateKey("social.recipientHealth", "number"),
            StateKey("social.recipientMood", "number"),
            StateKey("social.relationshipSentiment", "number"),
            StateKey("social.canForgive", "bool"),
            StateKey("social.identityRuleStateCount", "number"),
            StateKey("social.assaultExecuted", "bool"),
            StateKey("social.apologyExecuted", "bool"),
            StateKey("trait.ownerId", "string"),
            StateKey("trait.characterId", "string"),
            StateKey("trait.revision", "number"),
            StateKey("trait.activeCount", "number"),
            StateKey("trait.narrativeFactCount", "number"),
            StateKey("trait.sealQuantity", "number"),
            StateKey("trait.manifested", "bool"),
            StateKey("trait.reactionExecuted", "bool"),
            StateKey("trait.reactionTriggerCount", "number"),
            StateKey("trait.erased", "bool"),
            StateKey("trait.instanceId", "string"),
            StateKey("trait.level", "number"),
            StateKey("trait.currentExperience", "number"),
            StateKey("captivity.ownerId", "string"),
            StateKey("captivity.captiveId", "string"),
            StateKey("captivity.status", "string"),
            StateKey("captivity.will", "number"),
            StateKey("captivity.fear", "number"),
            StateKey("captivity.trust", "number"),
            StateKey("captivity.grudge", "number"),
            StateKey("captivity.performerFame", "number"),
            StateKey("captivity.performerSkill", "number"),
            StateKey("captivity.privilegeTier", "number"),
            StateKey("captivity.carePriorityUnlocked", "bool"),
            StateKey("captivity.interactionOutcomeRevision", "number"),
            StateKey("captivity.performerMilestoneOutcomeRevision", "number"),
            StateKey("captivity.ransomOutcomeRevision", "number"),
            StateKey("captivity.escapeOutcomeRevision", "number"),
            StateKey("captivity.retaliationPressure", "number"),
            StateKey("captivity.ransomAmount", "number"),
            StateKey("captivity.interactionResolved", "bool"),
            StateKey("captivity.milestoneUnlocked", "bool"),
            StateKey("captivity.ransomResolved", "bool"),
            StateKey("captivity.escaped", "bool"),
            StateKey("roomEnvironment.ownerId", "string"),
            StateKey("roomEnvironment.actorId", "string"),
            StateKey("roomEnvironment.facilityId", "string"),
            StateKey("roomEnvironment.roomId", "string"),
            StateKey("roomEnvironment.stage", "number"),
            StateKey("roomEnvironment.facilityCleanliness", "number"),
            StateKey("roomEnvironment.observedCleanliness", "number"),
            StateKey("roomEnvironment.evaluatedCleanliness", "number"),
            StateKey("roomEnvironment.impressiveness", "number"),
            StateKey("roomEnvironment.nextOutcomeSequence", "number"),
            StateKey("roomEnvironment.mood", "number"),
            StateKey("roomEnvironment.roomMoodFactorCount", "number"),
            StateKey("roomEnvironment.exactOutcomeCount", "number"),
            StateKey("roomEnvironment.experienceApplied", "bool"),
            StateKey("roomEnvironment.conditionChanged", "bool"),
            StateKey("speciesIncident.ownerId", "string"),
            StateKey("speciesIncident.actorId", "string"),
            StateKey("speciesIncident.facilityId", "string"),
            StateKey("speciesIncident.stage", "number"),
            StateKey("speciesIncident.speciesId", "string"),
            StateKey("speciesIncident.incidentId", "string"),
            StateKey("speciesIncident.incidentCount", "number"),
            StateKey("speciesIncident.nextIncidentAt", "number"),
            StateKey("speciesIncident.charge", "number"),
            StateKey("speciesIncident.integrity", "number"),
            StateKey("speciesIncident.actorHealth", "number"),
            StateKey("speciesIncident.facilityDamaged", "bool"),
            StateKey("speciesIncident.exactOutcomeCount", "number"),
            StateKey("speciesIncident.firstExecuted", "bool"),
            StateKey("speciesIncident.repeatExecuted", "bool"),
            StateKey("certifiedSeed.ownerId", "string"),
            StateKey("certifiedSeed.facilityId", "string"),
            StateKey("certifiedSeed.cropId", "string"),
            StateKey("certifiedSeed.stage", "number"),
            StateKey("certifiedSeed.nextOrderSequence", "number"),
            StateKey("certifiedSeed.lastProcessedOperatingDay", "number"),
            StateKey("certifiedSeed.operatingDay", "number"),
            StateKey("certifiedSeed.activeOrderCount", "number"),
            StateKey("certifiedSeed.certifiedLotCount", "number"),
            StateKey("certifiedSeed.exactOutcomeCount", "number"),
            StateKey("certifiedSeed.firstExecuted", "bool"),
            StateKey("certifiedSeed.repeatExecuted", "bool"),
            StateKey("staffResponse.ownerId", "string"),
            StateKey("staffResponse.rebelId", "string"),
            StateKey("staffResponse.workerId", "string"),
            StateKey("staffResponse.responderId", "string"),
            StateKey("staffResponse.stage", "number"),
            StateKey("staffResponse.outcomeRevision", "number"),
            StateKey("staffResponse.rebelStage", "string"),
            StateKey("staffResponse.rebelIsolated", "bool"),
            StateKey("staffResponse.workerStage", "string"),
            StateKey("staffResponse.workerMood", "number"),
            StateKey("staffResponse.workerLowMoodDays", "number"),
            StateKey("staffResponse.exactOutcomeCount", "number"),
            StateKey("staffResponse.isolationExecuted", "bool"),
            StateKey("staffResponse.calmExecuted", "bool"),
            StateKey("emergencySuspension.ownerId", "string"),
            StateKey("emergencySuspension.firstWorkerId", "string"),
            StateKey("emergencySuspension.secondWorkerId", "string"),
            StateKey("emergencySuspension.facilityId", "string"),
            StateKey("emergencySuspension.stage", "number"),
            StateKey("emergencySuspension.alertEpoch", "number"),
            StateKey("emergencySuspension.calendarAbsoluteHour", "number"),
            StateKey("emergencySuspension.suspendedWorkCount", "number"),
            StateKey("emergencySuspension.firstRecorded", "bool"),
            StateKey("emergencySuspension.firstCompletedWork", "number"),
            StateKey("emergencySuspension.firstRequiredWork", "number"),
            StateKey("emergencySuspension.secondRecorded", "bool"),
            StateKey("emergencySuspension.secondCompletedWork", "number"),
            StateKey("emergencySuspension.secondRequiredWork", "number"),
            StateKey("emergencySuspension.exactOutcomeCount", "number"),
            StateKey("bossAward.ownerId", "string"),
            StateKey("bossAward.firstRegionId", "string"),
            StateKey("bossAward.secondRegionId", "string"),
            StateKey("bossAward.stage", "number"),
            StateKey("bossAward.absoluteDay", "number"),
            StateKey("bossAward.awardedRegionCount", "number"),
            StateKey("bossAward.physicalPublicationCount", "number"),
            StateKey("bossAward.firstAwarded", "bool"),
            StateKey("bossAward.secondAwarded", "bool"),
            StateKey("bossAward.firstSealQuantity", "number"),
            StateKey("bossAward.secondSealQuantity", "number"),
            StateKey("bossAward.totalSealQuantity", "number"),
            StateKey("bossAward.exactOutcomeCount", "number"),
            StateKey("tradeInventory.ownerId", "string"),
            StateKey("tradeInventory.stage", "number"),
            StateKey("tradeInventory.absoluteDay", "number"),
            StateKey("tradeInventory.itemId", "string"),
            StateKey("tradeInventory.sourceStackId", "string"),
            StateKey("tradeInventory.relocatedStackId", "string"),
            StateKey("tradeInventory.sourceQuantity", "number"),
            StateKey("tradeInventory.relocatedQuantity", "number"),
            StateKey("tradeInventory.stackCount", "number"),
            StateKey("tradeInventory.relocatedPresent", "bool"),
            StateKey("tradeInventory.sourceX", "number"),
            StateKey("tradeInventory.sourceY", "number"),
            StateKey("tradeInventory.destinationX", "number"),
            StateKey("tradeInventory.destinationY", "number"),
            StateKey("tradeInventory.wasteOrigin", "string"),
            StateKey("tradeInventory.wasteDisposition", "string"),
            StateKey("tradeInventory.wasteEnabled", "bool"),
            StateKey("tradeInventory.wasteMaximumPercent", "number"),
            StateKey("tradeInventory.wasteNextOutcomeSequence", "number"),
            StateKey("tradeInventory.relocationOutcomeCount", "number"),
            StateKey("tradeInventory.dispositionOutcomeCount", "number"),
            StateKey("tradeInventory.wastePolicyOutcomeCount", "number"),
            StateKey("tradeInventory.exactOutcomeCount", "number"),
            StateKey("diseaseRoute.ownerId", "string"),
            StateKey("diseaseRoute.stage", "number"),
            StateKey("diseaseRoute.calendarDay", "number"),
            StateKey("diseaseRoute.lastPublishedAbsoluteDay", "number"),
            StateKey("diseaseRoute.actorId", "string"),
            StateKey("diseaseRoute.vectorId", "string"),
            StateKey("diseaseRoute.diseaseId", "string"),
            StateKey("diseaseRoute.healthCurrentAbsoluteDay", "number"),
            StateKey("diseaseRoute.healthVersion", "number"),
            StateKey("diseaseRoute.pendingExposureCount", "number"),
            StateKey("diseaseRoute.weightedExposureHours", "number"),
            StateKey("diseaseRoute.exactOutcomeCount", "number"),
            StateKey("diseaseRoute.lastOutcomeOperationId", "string"),
            StateKey("diseaseRoute.lastOutcomeCommitRevision", "number"),
            StateKey("diseaseRoute.lastOutcomeOwnerRevision", "number"),
            StateKey("offenseNode.ownerId", "string"),
            StateKey("offenseNode.stage", "number"),
            StateKey("offenseNode.absoluteDay", "number"),
            StateKey("offenseNode.expeditionId", "string"),
            StateKey("offenseNode.actorId", "string"),
            StateKey("offenseNode.targetKnown", "bool"),
            StateKey("offenseNode.currentNodeId", "string"),
            StateKey("offenseNode.completedNodeCount", "number"),
            StateKey("offenseNode.light", "number"),
            StateKey("offenseNode.rations", "number"),
            StateKey("offenseNode.tools", "number"),
            StateKey("offenseNode.consumedRations", "number"),
            StateKey("offenseNode.consumedTools", "number"),
            StateKey("offenseNode.generalLoot", "number"),
            StateKey("offenseNode.foodLoot", "number"),
            StateKey("offenseNode.actorLevel", "number"),
            StateKey("offenseNode.actorExperience", "number"),
            StateKey("offenseNode.actorStress", "number"),
            StateKey("offenseNode.actorNarrativeCount", "number"),
            StateKey("offenseNode.exactOutcomeCount", "number"),
            StateKey("offenseTruth.ownerId", "string"),
            StateKey("offenseTruth.stage", "number"),
            StateKey("offenseTruth.absoluteDay", "number"),
            StateKey("offenseTruth.knownTargetCount", "number"),
            StateKey("offenseTruth.completedTargetCount", "number"),
            StateKey("offenseTruth.preliminaryKnown", "bool"),
            StateKey("offenseTruth.preliminaryCompleted", "bool"),
            StateKey("offenseTruth.truthKnown", "bool"),
            StateKey("offenseTruth.truthCompleted", "bool"),
            StateKey("offenseTruth.truthRevealed", "bool"),
            StateKey("offenseTruth.revealedTargetId", "string"),
            StateKey("offenseTruth.migratedOwnerRevisionCount", "number"),
            StateKey("offenseTruth.preliminaryOutcomeCount", "number"),
            StateKey("offenseTruth.truthOutcomeCount", "number"),
            StateKey("offenseTruth.exactOutcomeCount", "number"),
            StateKey("workIdentity.ownerId", "string"),
            StateKey("workIdentity.characterId", "string"),
            StateKey("workIdentity.stage", "number"),
            StateKey("workIdentity.lastAppliedDay", "number"),
            StateKey("workIdentity.deliveryCursorCount", "number"),
            StateKey("workIdentity.deliveryOperationSequence", "number"),
            StateKey("workIdentity.identityRuleStateCount", "number"),
            StateKey("workIdentity.firstProcessRecorded", "bool"),
            StateKey("workIdentity.moodValue", "number"),
            StateKey("workIdentity.moodFactorCount", "number"),
            StateKey("workIdentity.firstApplied", "bool"),
            StateKey("workIdentity.secondApplied", "bool"),
            StateKey("workIdentity.lastProductId", "string"),
            StateKey("workIdentity.exactOutcomeCount", "number"),
            StateKey("runVariable.ownerId", "string"),
            StateKey("runVariable.stage", "number"),
            StateKey("runVariable.runSeed", "number"),
            StateKey("runVariable.currentDay", "number"),
            StateKey("runVariable.activeCount", "number"),
            StateKey("runVariable.activeVariableId", "string"),
            StateKey("runVariable.remainingDays", "number"),
            StateKey("runVariable.instanceSequence", "number"),
            StateKey("runVariable.nextOperationSequence", "number"),
            StateKey("runVariable.lastOperationAdvanceDay", "number"),
            StateKey("runVariable.activationOutcomeCount", "number"),
            StateKey("runVariable.expirationOutcomeCount", "number"),
            StateKey("runVariable.exactOutcomeCount", "number"),
            StateKey("ownerRun.ownerId", "string"),
            StateKey("visitorCheckout.ownerId", "string"),
            StateKey("visitorCheckout.stage", "number"),
            StateKey("visitorCheckout.absoluteDay", "number"),
            StateKey("visitorCheckout.actorId", "string"),
            StateKey("visitorCheckout.actorName", "string"),
            StateKey("visitorCheckout.workerId", "string"),
            StateKey("visitorCheckout.shopId", "string"),
            StateKey("visitorCheckout.incidentId", "string"),
            StateKey("visitorCheckout.receptionApplied", "bool"),
            StateKey("visitorCheckout.receptionMood", "number"),
            StateKey("visitorCheckout.receptionRemainingSeconds", "number"),
            StateKey("visitorCheckout.incidentRemainingSeconds", "number"),
            StateKey("visitorCheckout.incidentProgress", "number"),
            StateKey("visitorCheckout.stockQuantity", "number"),
            StateKey("visitorCheckout.completedUses", "number"),
            StateKey("visitorCheckout.holdingMoney", "number"),
            StateKey("visitorCheckout.currentUsers", "number"),
            StateKey("visitorCheckout.crimeDraws", "number"),
            StateKey("visitorCheckout.crimeChance", "number"),
            StateKey("visitorCheckout.crimeRoll", "number"),
            StateKey("visitorCheckout.exactOutcomeCount", "number"),
            StateKey("visitorCheckout.clockTime", "number"),
            StateKey("visitorCheckout.clockFrame", "number"),
            StateKey("tabooButchery.ownerId", "string"),
            StateKey("tabooButchery.stage", "number"),
            StateKey("tabooButchery.absoluteDay", "number"),
            StateKey("tabooButchery.butcherId", "string"),
            StateKey("tabooButchery.butcherName", "string"),
            StateKey("tabooButchery.butcherSpecies", "string"),
            StateKey("tabooButchery.victimId", "string"),
            StateKey("tabooButchery.victimName", "string"),
            StateKey("tabooButchery.victimSpecies", "string"),
            StateKey("tabooButchery.victimHealth", "number"),
            StateKey("tabooButchery.victimDead", "bool"),
            StateKey("tabooButchery.witnessId", "string"),
            StateKey("tabooButchery.witnessName", "string"),
            StateKey("tabooButchery.witnessSpecies", "string"),
            StateKey("tabooButchery.corpseStackId", "string"),
            StateKey("tabooButchery.corpseQuantity", "number"),
            StateKey("tabooButchery.corpseSourceId", "string"),
            StateKey("tabooButchery.corpseSourceName", "string"),
            StateKey("tabooButchery.corpseSourceSpecies", "string"),
            StateKey("tabooButchery.corpseDeathReason", "string"),
            StateKey("tabooButchery.corpseAuthorized", "bool"),
            StateKey("tabooButchery.bloodAmount", "number"),
            StateKey("tabooButchery.meatQuantity", "number"),
            StateKey("tabooButchery.boneQuantity", "number"),
            StateKey("tabooButchery.butcherHygiene", "number"),
            StateKey("tabooButchery.butcherGuilt", "number"),
            StateKey("tabooButchery.butcherTabooMemoryCount", "number"),
            StateKey("tabooButchery.witnessMood", "number"),
            StateKey("tabooButchery.witnessFactCount", "number"),
            StateKey("tabooButchery.witnessRumorCount", "number"),
            StateKey("tabooButchery.exactOutcomeCount", "number"),
            StateKey("tabooButchery.physicalStackCursor", "number"),
            StateKey("ownerRun.stage", "number"),
            StateKey("ownerRun.runSeed", "number"),
            StateKey("ownerRun.absoluteDay", "number"),
            StateKey("ownerRun.actorId", "string"),
            StateKey("ownerRun.actorDataId", "number"),
            StateKey("ownerRun.actorName", "string"),
            StateKey("ownerRun.phase", "string"),
            StateKey("ownerRun.outcome", "string"),
            StateKey("ownerRun.ownerEnded", "bool"),
            StateKey("ownerRun.settlementEnded", "bool"),
            StateKey("ownerRun.completedRunCount", "number"),
            StateKey("ownerRun.earnedCurrency", "number"),
            StateKey("ownerRun.spentCurrency", "number"),
            StateKey("ownerRun.availableCurrency", "number"),
            StateKey("ownerRun.upgradeId", "string"),
            StateKey("ownerRun.upgradeCost", "number"),
            StateKey("ownerRun.upgradeLevel", "number"),
            StateKey("ownerRun.facilityCandidateBonus", "number"),
            StateKey("ownerRun.ownerOutcomeCount", "number"),
            StateKey("ownerRun.settlementOutcomeCount", "number"),
            StateKey("ownerRun.upgradeOutcomeCount", "number"),
            StateKey("ownerRun.exactOutcomeCount", "number"),
            StateKey("metaProgression.ownerId", "string"),
            StateKey("metaProgression.stage", "number"),
            StateKey("metaProgression.lifetimeEarnedCurrency", "number"),
            StateKey("metaProgression.spentCurrency", "number"),
            StateKey("metaProgression.availableCurrency", "number"),
            StateKey("metaProgression.completedRunCount", "number"),
            StateKey("metaProgression.ended", "bool"),
            StateKey("metaProgression.hasLatestResult", "bool"),
            StateKey("metaProgression.latestOwnerName", "string"),
            StateKey("metaProgression.latestEndReason", "string"),
            StateKey("metaProgression.latestOutcome", "string"),
            StateKey("metaProgression.latestLegacyCurrency", "number"),
            StateKey("metaProgression.upgradeId", "string"),
            StateKey("metaProgression.upgradeLevel", "number"),
            StateKey("metaProgression.runResultOutcomeCount", "number"),
            StateKey("metaProgression.upgradeOutcomeCount", "number"),
            StateKey("metaProgression.exactOutcomeCount", "number"),
            StateKey("regularCustomer.ownerId", "string"),
            StateKey("regularCustomer.stage", "number"),
            StateKey("regularCustomer.absoluteDay", "number"),
            StateKey("regularCustomer.customerId", "string"),
            StateKey("regularCustomer.displayName", "string"),
            StateKey("regularCustomer.visitCount", "number"),
            StateKey("regularCustomer.averageSatisfaction", "number"),
            StateKey("regularCustomer.status", "string"),
            StateKey("regularCustomer.isRegular", "bool"),
            StateKey("regularCustomer.isRecruitCandidate", "bool"),
            StateKey("regularCustomer.isRecruited", "bool"),
            StateKey("regularCustomer.recruitedAbsoluteDay", "number"),
            StateKey("regularCustomer.actorType", "string"),
            StateKey("regularCustomer.deliveryPending", "bool"),
            StateKey("regularCustomer.visitOutcomeCount", "number"),
            StateKey("regularCustomer.recruitOutcomeCount", "number"),
            StateKey("regularCustomer.exactOutcomeCount", "number"),
            StateKey("characterLife.ownerId", "string"),
            StateKey("characterLife.stage", "number"),
            StateKey("characterLife.absoluteDay", "number"),
            StateKey("characterLife.characterId", "string"),
            StateKey("characterLife.chronologicalAgeDays", "number"),
            StateKey("characterLife.biologicalAgeDayUnits", "number"),
            StateKey("characterLife.lifeStage", "string"),
            StateKey("characterLife.conditionId", "string"),
            StateKey("characterLife.conditionSeverity", "number"),
            StateKey("characterLife.conditionNextProgressDayUnits", "number"),
            StateKey("characterLife.ageConditionOutcomeCount", "number"),
            StateKey("characterLife.lifeStageOutcomeCount", "number"),
            StateKey("characterLife.exactOutcomeCount", "number"),
            StateKey("factionRoute.ownerId", "string"),
            StateKey("factionRoute.stage", "number"),
            StateKey("factionRoute.absoluteDay", "number"),
            StateKey("factionRoute.factionId", "string"),
            StateKey("factionRoute.routeId", "string"),
            StateKey("factionRoute.routeCount", "number"),
            StateKey("factionRoute.routeStatus", "string"),
            StateKey("factionRoute.pathIndex", "number"),
            StateKey("factionRoute.segmentProgress", "number"),
            StateKey("factionRoute.settlementState", "string"),
            StateKey("factionRoute.settlementPaymentGold", "number"),
            StateKey("factionRoute.settlementBalanceBefore", "number"),
            StateKey("factionRoute.settlementBalanceAfter", "number"),
            StateKey("factionRoute.cargoDeliveryState", "string"),
            StateKey("factionRoute.cargoStackCount", "number"),
            StateKey("factionRoute.cargoDeliveredQuantity", "number"),
            StateKey("factionRoute.sessionMoney", "number"),
            StateKey("factionRoute.settlementOutcomeCount", "number"),
            StateKey("factionRoute.arrivalOutcomeCount", "number"),
            StateKey("factionRoute.cargoOutcomeCount", "number"),
            StateKey("factionRoute.exactOutcomeCount", "number"),
            StateKey("consumables.ownerId", "string"),
            StateKey("consumables.stage", "number"),
            StateKey("consumables.absoluteDay", "number"),
            StateKey("consumables.characterId", "string"),
            StateKey("consumables.completedOperationCount", "number"),
            StateKey("consumables.substanceStateCount", "number"),
            StateKey("consumables.substanceTolerance", "number"),
            StateKey("consumables.hungerRecovered", "number"),
            StateKey("consumables.moodApplied", "number"),
            StateKey("consumables.damageApplied", "number"),
            StateKey("consumables.acknowledgementCount", "number"),
            StateKey("consumables.pendingCommitCount", "number"),
            StateKey("consumables.mealOutcomeCount", "number"),
            StateKey("consumables.substanceOutcomeCount", "number"),
            StateKey("consumables.exactOutcomeCount", "number"),
            StateKey("mealShortage.ownerId", "string"),
            StateKey("mealShortage.stage", "number"),
            StateKey("mealShortage.characterId", "string"),
            StateKey("mealShortage.lastProcessedDay", "number"),
            StateKey("mealShortage.neededFood", "number"),
            StateKey("mealShortage.consumedFood", "number"),
            StateKey("mealShortage.missingFood", "number"),
            StateKey("mealShortage.neededWater", "number"),
            StateKey("mealShortage.consumedWater", "number"),
            StateKey("mealShortage.missingWater", "number"),
            StateKey("mealShortage.foodShortageDays", "number"),
            StateKey("mealShortage.waterShortageDays", "number"),
            StateKey("mealShortage.sanitationRisk", "number"),
            StateKey("mealShortage.diseaseRisk", "number"),
            StateKey("mealShortage.exteriorNightDanger", "number"),
            StateKey("mealShortage.mealLedgerCount", "number"),
            StateKey("mealShortage.healthStateCount", "number"),
            StateKey("mealShortage.outcomeCount", "number"),
            StateKey("mealShortage.exactOutcomeCount", "number"),
            StateKey("primitiveSurvival.ownerId", "string"),
            StateKey("primitiveSurvival.stage", "number"),
            StateKey("primitiveSurvival.characterId", "string"),
            StateKey("primitiveSurvival.calendarDay", "number"),
            StateKey("primitiveSurvival.sleep", "number"),
            StateKey("primitiveSurvival.hygiene", "number"),
            StateKey("primitiveSurvival.excretion", "number"),
            StateKey("primitiveSurvival.baseMood", "number"),
            StateKey("primitiveSurvival.moodFactorCount", "number"),
            StateKey("primitiveSurvival.floorRestMood", "number"),
            StateKey("primitiveSurvival.outcomeOwnerRevision", "number"),
            StateKey("primitiveSurvival.outcomeCount", "number"),
            StateKey("primitiveSurvival.exactOutcomeCount", "number"),
            StateKey("serviceHubDay.ownerId", "string"),
            StateKey("serviceHubDay.stage", "number"),
            StateKey("serviceHubDay.facilityId", "string"),
            StateKey("serviceHubDay.visitorId", "string"),
            StateKey("serviceHubDay.calendarDay", "number"),
            StateKey("serviceHubDay.completedUses", "number"),
            StateKey("serviceHubDay.serviceMode", "string"),
            StateKey("serviceHubDay.settlementDay", "number"),
            StateKey("serviceHubDay.settlementVisits", "number"),
            StateKey("serviceHubDay.reportCount", "number"),
            StateKey("serviceHubDay.latestReportDay", "number"),
            StateKey("serviceHubDay.latestReportVisits", "number"),
            StateKey("serviceHubDay.money", "number"),
            StateKey("serviceHubDay.visitOwnerRevision", "number"),
            StateKey("serviceHubDay.modeOwnerRevision", "number"),
            StateKey("serviceHubDay.dayOwnerRevision", "number"),
            StateKey("serviceHubDay.exactOutcomeCount", "number"),
            StateKey("invasionLifecycle.ownerId", "string"),
            StateKey("invasionLifecycle.stage", "number"),
            StateKey("invasionLifecycle.runtimeId", "string"),
            StateKey("invasionLifecycle.ownerCharacterId", "string"),
            StateKey("invasionLifecycle.facilityId", "string"),
            StateKey("invasionLifecycle.calendarDay", "number"),
            StateKey("invasionLifecycle.activeIntruders", "number"),
            StateKey("invasionLifecycle.intruderState", "string"),
            StateKey("invasionLifecycle.intruderHealth", "number"),
            StateKey("invasionLifecycle.hasBreachedDungeon", "bool"),
            StateKey("invasionLifecycle.facilityDamageCount", "number"),
            StateKey("invasionLifecycle.facilityDamaged", "bool"),
            StateKey("invasionLifecycle.frontCollapsed", "bool"),
            StateKey("invasionLifecycle.startOwnerRevision", "number"),
            StateKey("invasionLifecycle.breachOwnerRevision", "number"),
            StateKey("invasionLifecycle.damageOwnerRevision", "number"),
            StateKey("invasionLifecycle.frontCollapseOwnerRevision", "number"),
            StateKey("invasionLifecycle.resolveOwnerRevision", "number"),
            StateKey("invasionLifecycle.resolutionStatus", "string"),
            StateKey("invasionLifecycle.exactOutcomeCount", "number"),
            StateKey("equipment.ownerId", "string"),
            StateKey("equipment.characterId", "string"),
            StateKey("equipment.facilityId", "string"),
            StateKey("equipment.apparelDefinitionId", "string"),
            StateKey("equipment.materialQuantity", "number"),
            StateKey("equipment.outputQuantity", "number"),
            StateKey("equipment.outputStackId", "string"),
            StateKey("equipment.outputInstanceId", "string"),
            StateKey("equipment.outputState", "string"),
            StateKey("equipment.apparelDurability", "number"),
            StateKey("equipment.apparelVersion", "number"),
            StateKey("equipment.equippedCount", "number"),
            StateKey("equipment.crafted", "bool"),
            StateKey("equipment.equipped", "bool"),
            StateKey("research.ownerId", "string"),
            StateKey("research.facilityId", "string"),
            StateKey("research.taskId", "string"),
            StateKey("research.sourceStackId", "string"),
            StateKey("research.completedWork", "number"),
            StateKey("research.requiredWork", "number"),
            StateKey("research.intelligenceDamage", "number"),
            StateKey("research.outcomeSequence", "number"),
            StateKey("research.sourceAvailable", "bool"),
            StateKey("research.progressApplied", "bool"),
            StateKey("research.completed", "bool"),
            StateKey("production.ownerId", "string"),
            StateKey("production.facilityId", "string"),
            StateKey("production.workerId", "string"),
            StateKey("production.recipeId", "string"),
            StateKey("production.billId", "string"),
            StateKey("production.billCount", "number"),
            StateKey("production.commandOutcomeSequence", "number"),
            StateKey("production.cycleSequence", "number"),
            StateKey("production.remainingCycles", "number"),
            StateKey("production.completedWork", "number"),
            StateKey("production.preparedOutputPhase", "string"),
            StateKey("production.outputQuantity", "number"),
            StateKey("production.outputMassGrams", "number"),
            StateKey("production.commandApplied", "bool"),
            StateKey("production.completed", "bool"),
            StateKey("facility.ownerId", "string"),
            StateKey("facility.instanceId", "string"),
            StateKey("facility.definitionId", "string"),
            StateKey("facility.displayName", "string"),
            StateKey("facility.starGrade", "number"),
            StateKey("facility.historyCount", "number"),
            StateKey("facility.generation", "number"),
            StateKey("facility.mercenaryTokenCount", "number"),
            StateKey("facility.mutationTagCount", "number"),
            StateKey("facility.exactOutcomeCount", "number"),
            StateKey("facility.evolutionStage", "number"),
            StateKey("facility.firstCompleted", "bool"),
            StateKey("facility.secondCompleted", "bool"),
            StateKey("facilitySynthesis.ownerId", "string"),
            StateKey("facilitySynthesis.stage", "number"),
            StateKey("facilitySynthesis.survivorId", "string"),
            StateKey("facilitySynthesis.definitionId", "string"),
            StateKey("facilitySynthesis.displayName", "string"),
            StateKey("facilitySynthesis.facilityLevel", "number"),
            StateKey("facilitySynthesis.outcomeRevision", "number"),
            StateKey("facilitySynthesis.liveBuildingCount", "number"),
            StateKey("facilitySynthesis.exactOutcomeCount", "number"),
            StateKey("facilitySynthesis.battleBannerUnlocked", "bool"),
            StateKey("facilitySynthesis.patrolBoardCompleted", "bool"),
            StateKey("facilitySynthesis.battleBannerCompleted", "bool"),
            StateKey("infrastructure.ownerId", "string"),
            StateKey("infrastructure.facilityId", "string"),
            StateKey("infrastructure.displayName", "string"),
            StateKey("infrastructure.connectionEnabled", "bool"),
            StateKey("infrastructure.powerNodeCount", "number"),
            StateKey("infrastructure.commandNextOwnerRevision", "number"),
            StateKey("infrastructure.pendingOutcomeCount", "number"),
            StateKey("infrastructure.exactOutcomeCount", "number"),
            StateKey("infrastructure.stage", "number"),
            StateKey("infrastructure.disconnected", "bool"),
            StateKey("infrastructure.reconnected", "bool"),
            StateKey("dungeonSpace.ownerId", "string"),
            StateKey("dungeonSpace.stage", "number"),
            StateKey("dungeonSpace.tier", "number"),
            StateKey("dungeonSpace.interiorColumns", "number"),
            StateKey("dungeonSpace.gridWidth", "number"),
            StateKey("dungeonSpace.entranceX", "number"),
            StateKey("dungeonSpace.entranceY", "number"),
            StateKey("dungeonSpace.exactOutcomeCount", "number"),
            StateKey("dungeonSpace.quarryCompleted", "bool"),
            StateKey("dungeonSpace.stonecuttingCompleted", "bool"),
            StateKey("stockTransform.ownerId", "string"),
            StateKey("stockTransform.stage", "number"),
            StateKey("stockTransform.absoluteDay", "number"),
            StateKey("stockTransform.money", "number"),
            StateKey("stockTransform.sourceItemId", "string"),
            StateKey("stockTransform.sourceStackId", "string"),
            StateKey("stockTransform.sourceQuantity", "number"),
            StateKey("stockTransform.sourceX", "number"),
            StateKey("stockTransform.sourceY", "number"),
            StateKey("stockTransform.outputItemId", "string"),
            StateKey("stockTransform.outputQuantity", "number"),
            StateKey("stockTransform.stackCount", "number"),
            StateKey("stockTransform.supplyOutcomeCount", "number"),
            StateKey("stockTransform.transformOutcomeCount", "number"),
            StateKey("stockTransform.migratedOwnerCount", "number"),
            StateKey("stockTransform.exactOutcomeCount", "number"),
            StateKey("facilityProcurement.ownerId", "string"),
            StateKey("facilityProcurement.stage", "number"),
            StateKey("facilityProcurement.absoluteDay", "number"),
            StateKey("facilityProcurement.money", "number"),
            StateKey("facilityProcurement.lastProcessedDay", "number"),
            StateKey("facilityProcurement.dailyBudget", "number"),
            StateKey("facilityProcurement.minimumReserve", "number"),
            StateKey("facilityProcurement.wishlistRuleCount", "number"),
            StateKey("facilityProcurement.resultCount", "number"),
            StateKey("facilityProcurement.purchasedResultCount", "number"),
            StateKey("facilityProcurement.processedOfferKeyCount", "number"),
            StateKey("facilityProcurement.buildingId", "number"),
            StateKey("facilityProcurement.facilityCost", "number"),
            StateKey("facilityProcurement.buildingUnlockedCount", "number"),
            StateKey("facilityProcurement.installationKitCount", "number"),
            StateKey("facilityProcurement.autoOutcomeCount", "number"),
            StateKey("facilityProcurement.facilityOutcomeCount", "number"),
            StateKey("facilityProcurement.migratedOwnerCount", "number"),
            StateKey("facilityProcurement.exactOutcomeCount", "number"),
            StateKey("careerArc.ownerId", "string"),
            StateKey("careerArc.stage", "number"),
            StateKey("careerArc.absoluteDay", "number"),
            StateKey("careerArc.mentorId", "string"),
            StateKey("careerArc.studentId", "string"),
            StateKey("careerArc.academyId", "string"),
            StateKey("careerArc.proficiencyId", "string"),
            StateKey("careerArc.successfulReceiptCount", "number"),
            StateKey("careerArc.activeEventCount", "number"),
            StateKey("careerArc.resolvedEventCount", "number"),
            StateKey("careerArc.mentorshipAwardOutcomeCount", "number"),
            StateKey("careerArc.lastLessonOutcomeCount", "number"),
            StateKey("careerArc.quietPromotionOutcomeCount", "number"),
            StateKey("careerArc.funeralOutcomeCount", "number"),
            StateKey("careerArc.lineageCompressionOutcomeCount", "number"),
            StateKey("careerArc.mentorTombstoneCount", "number"),
            StateKey("careerArc.lineageSummaryCount", "number"),
            StateKey("careerArc.lineageWorldFlagCount", "number"),
            StateKey("careerArc.migratedOwnerCount", "number"),
            StateKey("careerArc.exactOutcomeCount", "number"),
            StateKey("mealIncident.ownerId", "string"),
            StateKey("mealIncident.stage", "number"),
            StateKey("mealIncident.absoluteDay", "number"),
            StateKey("mealIncident.customerId", "string"),
            StateKey("mealIncident.itemDefinitionId", "string"),
            StateKey("mealIncident.itemStackId", "string"),
            StateKey("mealIncident.definitionId", "string"),
            StateKey("mealIncident.occurrenceId", "string"),
            StateKey("mealIncident.resolutionId", "string"),
            StateKey("mealIncident.observedCause", "string"),
            StateKey("mealIncident.successfulReceiptCount", "number"),
            StateKey("mealIncident.activeEventCount", "number"),
            StateKey("mealIncident.resolvedEventCount", "number"),
            StateKey("mealIncident.workDelayDays", "number"),
            StateKey("mealIncident.observationOutcomeCount", "number"),
            StateKey("mealIncident.resolutionOutcomeCount", "number"),
            StateKey("mealIncident.migratedOwnerCount", "number"),
            StateKey("mealIncident.exactOutcomeCount", "number"),
            StateKey("offenseArrival.ownerId", "string"),
            StateKey("offenseArrival.stage", "number"),
            StateKey("offenseArrival.day", "number"),
            StateKey("offenseArrival.expeditionId", "string"),
            StateKey("offenseArrival.targetId", "string"),
            StateKey("offenseArrival.targetTitle", "string"),
            StateKey("offenseArrival.authoredRewardType", "string"),
            StateKey("offenseArrival.arrivalId", "string"),
            StateKey("offenseArrival.arrivalKind", "string"),
            StateKey("offenseArrival.arrivalStage", "string"),
            StateKey("offenseArrival.requestedAmount", "number"),
            StateKey("offenseArrival.returnSealed", "bool"),
            StateKey("offenseArrival.materializedCount", "number"),
            StateKey("offenseArrival.escapedCount", "number"),
            StateKey("offenseArrival.liveWildlifeCount", "number"),
            StateKey("offenseArrival.materializedWildlifeId", "string"),
            StateKey("offenseArrival.materializedSpeciesId", "string"),
            StateKey("offenseArrival.materializedMaxHealth", "number"),
            StateKey("offenseArrival.materializedArrivalHealth", "number"),
            StateKey("offenseArrival.resultHistoryCount", "number"),
            StateKey("offenseArrival.resultReceiptResolution", "string"),
            StateKey("offenseArrival.resultReceiptMaterialized", "number"),
            StateKey("offenseArrival.resultReceiptSecured", "number"),
            StateKey("offenseArrival.resultReceiptEscaped", "number"),
            StateKey("offenseArrival.metaOffenseSuccessCount", "number"),
            StateKey("offenseArrival.campaignTargetCompleted", "bool"),
            StateKey("offenseArrival.resultOutcomeCount", "number"),
            StateKey("offenseArrival.arrivalOutcomeCount", "number"),
            StateKey("offenseArrival.exactOutcomeCount", "number"),
            StateKey("festival.ownerId", "string"),
            StateKey("festival.stage", "number"),
            StateKey("festival.festivalId", "string"),
            StateKey("festival.displayName", "string"),
            StateKey("festival.currentYear", "number"),
            StateKey("festival.currentAbsoluteDay", "number"),
            StateKey("festival.participantCount", "number"),
            StateKey("festival.occurrenceCount", "number"),
            StateKey("festival.resolvedOccurrenceCount", "number"),
            StateKey("festival.successfulAttendanceCount", "number"),
            StateKey("festival.candleQuantity", "number"),
            StateKey("festival.activeInputOwnerCount", "number"),
            StateKey("festival.pendingMaterialReceiptCount", "number"),
            StateKey("festival.cleanupCompleteCount", "number"),
            StateKey("festival.moodFactorCount", "number"),
            StateKey("festival.attendanceRecordCount", "number"),
            StateKey("festival.griefRemainingMultiplier", "number"),
            StateKey("festival.factionRapportTotal", "number"),
            StateKey("festival.exactOutcomeCount", "number"),
            StateKey("festival.controlledMaterialPlacement", "bool"),
            StateKey("festival.naturalDeliveryTelemetry", "bool")
        },
        ["actions"] = new JArray
        {
            Action(
                "action:drink-clean-water",
                "survival",
                "CharacterWaterConsumptionCoordinator.TryConsumeWorldSource",
                MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
                new[] { "actor.thirst", "world.cleanWaterRemaining" },
                new[] { "role:protagonist" },
                new[] { "survival-recovery" }),
            Action(
                "action:rest",
                "life",
                "CharacterStats.RecoverNeed",
                MigratedProducerOutcomeKind.RestOutcomeIdentityEvent,
                new[] { "actor.sleep" },
                new[] { "role:protagonist" },
                new[] { "rest-recovery" }),
            Action(
                "action:train-proficiency",
                "career",
                "CharacterNarrativeRuntime.AddDirectExperience",
                MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt,
                new[] { "actor.proficiencyMilliExperience" },
                new[] { "role:protagonist" },
                new[] { "career-growth" }),
            Action(
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .NonlethalCombatInjuryActionId,
                "combat-injury",
                "CombatCommandResultApplier.Apply",
                MigratedProducerOutcomeKind.CombatAttackResult,
                CombatLifeCycleStateKeys(),
                new[] { "role:combat-enemy", "role:protagonist" },
                new[] { "combat-injury", "health-threshold", "combat-downed" },
                new[]
                {
                    MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
                    MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent
                }),
            Action(
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .RecoverBodyHealthActionId,
                "injury-treatment",
                "CharacterBodyHealthRuntime.Heal",
                MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent,
                CombatLifeCycleStateKeys(),
                new[] { "role:protagonist" },
                new[] { "physical-recovery", "health-threshold-recovery" },
                new[]
                {
                    MigratedProducerOutcomeKind.HealthThresholdCrossedEvent
                }),
            Action(
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .FatalCombatInjuryActionId,
                "combat-fatality",
                "CombatCommandResultApplier.Apply",
                MigratedProducerOutcomeKind.CombatAttackResult,
                CombatLifeCycleStateKeys(),
                new[] { "role:combat-enemy", "role:protagonist" },
                new[]
                {
                    "combat-fatality", "health-threshold", "combat-downed",
                    "character-death", "combat-kill"
                },
                new[]
                {
                    MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
                    MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent,
                    MigratedProducerOutcomeKind.CharacterDeathEvent,
                    MigratedProducerOutcomeKind.CharacterKilledEvent
                }),
            Action(
                StockSupplyTransformCausalProductionEntryDebugScenarios
                    .SupplyActionId,
                "inventory",
                "WarehouseFeatureCommandService.PurchaseDelivery",
                MigratedProducerOutcomeKind.StockSupplyResult,
                StockSupplyTransformStateKeys(),
                new[] { "role:stock-supply-lumber" },
                new[] { "stock-supply", "physical-delivery" }),
            Action(
                StockSupplyTransformCausalProductionEntryDebugScenarios
                    .TransformActionId,
                "inventory",
                "PhysicalItemTransformService.TryTransformQuantity",
                MigratedProducerOutcomeKind.PhysicalItemTransformReceipt,
                StockSupplyTransformStateKeys(),
                new[]
                {
                    "role:stock-supply-lumber",
                    "role:stock-transform-output"
                },
                new[] { "physical-transform", "supplied-input" }),
            Action(
                FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                    .BudgetBlockedActionId,
                "economy",
                "AutoProcurementRuntime.ProcessShopRefresh",
                MigratedProducerOutcomeKind.AutoProcurementResult,
                FacilityWishlistProcurementStateKeys(),
                new[] { "role:facility-procurement-policy" },
                new[] { "auto-procurement", "budget-blocked" }),
            Action(
                FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                    .PurchaseWishlistActionId,
                "economy",
                "DailyFacilityShopRuntime.Refresh",
                MigratedProducerOutcomeKind.AutoProcurementResult,
                FacilityWishlistProcurementStateKeys(),
                new[]
                {
                    "role:facility-procurement-policy",
                    "role:facility-procurement-target"
                },
                new[]
                {
                    "auto-procurement", "facility-wishlist-purchase",
                    "physical-delivery"
                },
                new[]
                {
                    MigratedProducerOutcomeKind.FacilityShopPurchaseResult
                }),
            Action(
                ObservedCareerArcCausalProductionEntryDebugScenarios
                    .MentorshipAwardActionId,
                "character",
                "CareerDurableEquipmentAwardRuntime.TryCommitAward",
                MigratedProducerOutcomeKind
                    .CareerMentorshipAwardCommitReceipt,
                ObservedCareerArcStateKeys(),
                new[] { "role:career-mentor", "role:career-student" },
                new[] { "career-mentorship-award", "durable-career-ledger" }),
            Action(
                ObservedCareerArcCausalProductionEntryDebugScenarios
                    .LastLessonActionId,
                "character",
                "ObservedLifeEventOutcomeCoordinator.TryRecordLastLesson",
                MigratedProducerOutcomeKind
                    .ObservedLastLessonLifeEventReceipt,
                ObservedCareerArcStateKeys(),
                new[] { "role:career-mentor", "role:career-student" },
                new[] { "career-last-lesson", "mentorship" }),
            Action(
                ObservedCareerArcCausalProductionEntryDebugScenarios
                    .QuietPromotionActionId,
                "character",
                "ObservedLifeEventOutcomeCoordinator.TryRecordQuietPromotion",
                MigratedProducerOutcomeKind
                    .ObservedQuietPromotionLifeEventReceipt,
                ObservedCareerArcStateKeys(),
                new[] { "role:career-student" },
                new[] { "career-promotion", "proficiency-growth" }),
            Action(
                ObservedCareerArcCausalProductionEntryDebugScenarios
                    .FuneralActionId,
                "character",
                "ObservedLifeEventOutcomeCoordinator.TryRecordFuneral",
                MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt,
                ObservedCareerArcStateKeys(),
                new[] { "role:career-mentor", "role:career-student" },
                new[] { "mentor-funeral", "career-memory" }),
            Action(
                ObservedCareerArcCausalProductionEntryDebugScenarios
                    .LineageCompressionActionId,
                "character",
                "KinshipHouseholdRuntime.ArchiveColdData",
                MigratedProducerOutcomeKind
                    .ObservedLineageCompressionLifeEventReceipt,
                ObservedCareerArcStateKeys(),
                new[] { "role:career-mentor", "role:career-student" },
                new[] { "lineage-compression", "career-legacy" }),
            Action(
                ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                    .ObserveIncidentActionId,
                "survival",
                "V20CampaignRuntime.CaptureObservedMealIncident",
                MigratedProducerOutcomeKind.ObservedMealIncidentCaptureResult,
                ObservedMealIncidentResolutionStateKeys(),
                new[] { "role:meal-incident-customer" },
                new[] { "observed-contamination", "meal-incident" }),
            Action(
                ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                    .ResolveIncidentActionId,
                "run",
                "V20ContentResolutionService.TryExecute",
                MigratedProducerOutcomeKind.V20ResolvedEventResult,
                ObservedMealIncidentResolutionStateKeys(),
                new[] { "role:meal-incident-customer" },
                new[] { "campaign-event-resolution", "quarantine" }),
            Action(
                "action:resolve-combat-attack",
                "combat",
                "CombatCommandResultApplier.Apply",
                MigratedProducerOutcomeKind.CombatAttackResult,
                new[] { "combat.ownerSnapshot" },
                new[] { "role:combat-attacker", "role:combat-victim" },
                new[] { "combat-resolution" }),
            Action(
                "action:trigger-defense-facility",
                "facility",
                "DefenseFacility.Trigger",
                MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent,
                new[] { "facility.ownerSnapshot" },
                new[] { "role:defense-facility", "role:defense-target" },
                new[] { "facility-defense" },
                new[]
                {
                    MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent
                }),
            Action(
                "action:adjust-faction-trust",
                "relationship",
                "FactionRuntimeApplicationAdapter.TryOfferGoodwill",
                MigratedProducerOutcomeKind.FactionTrustChangedEvent,
                new[] { "relationship.ownerSnapshot" },
                new[] { "role:faction" },
                new[] { "relationship-change" }),
            Action(
                "action:execute-offense-battle-command",
                "combat",
                "OffenseBattleRuntime.TryExecutePlannedCommand",
                MigratedProducerOutcomeKind.OffenseBattleCommandResult,
                new[] { "offense.ownerSnapshot" },
                new[] { "role:offense-ally", "role:offense-enemy" },
                new[] { "offense-resolution" }),
            Action(
                "action:apply-detox-treatment",
                "injury-treatment",
                "CharacterConsumablesRuntime.TryApplyDetoxTreatment",
                MigratedProducerOutcomeKind.CharacterDetoxTreatmentResult,
                new[] { "treatment.ownerSnapshot" },
                new[] { "role:treatment-subject" },
                new[] { "detox-treatment" }),
            Action(
                "action:purchase-retail-item",
                "economy",
                "AbilityShopping.BuyItem",
                MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult,
                new[]
                {
                    "economy.stockCount",
                    "economy.actorMoney",
                    "economy.purchaseCount"
                },
                new[] { "role:retail-customer", "role:retail-facility" },
                new[] { "retail-purchase" }),
            Action(
                "action:environment-crop-irrigation-supplied",
                "environment",
                "CropPlotRuntime.Tick",
                "CropIrrigationSupplyResult",
                EnvironmentOutcomeIds.CropIrrigationSupplied,
                new[]
                {
                    "crop.plotId",
                    "crop.phase",
                    "crop.currentWater",
                    "crop.growthProgress",
                    "crop.sourceWaterRemaining"
                },
                new[] { "role:crop-plot", "role:irrigation-facility" },
                new[] { "crop-irrigation" }),
            Action(
                "action:environment-crop-plan-terminal",
                "environment",
                "CropPlotRuntime.ApplyWork",
                "CropPlanExecutionReceipt",
                EnvironmentOutcomeIds.CropPlanTerminal,
                new[]
                {
                    "crop.plotId",
                    "crop.phase",
                    "crop.growthProgress",
                    "crop.harvestItemCount"
                },
                new[] { "role:crop-plot" },
                new[] { "crop-harvest" }),
            Action(
                "action:environment-fire-ignite",
                "environment",
                "EnvironmentalFireRuntime.TryIgnite",
                "EnvironmentalFireIgnitionResult",
                EnvironmentOutcomeIds.FireIgnition,
                new[]
                {
                    "fire.phase", "fire.fireId", "fire.intensity",
                    "fire.plainIgnited"
                },
                new[] { "role:fire-target" },
                new[] { "fire-ignition" }),
            Action(
                "action:environment-fire-initial-attack",
                "environment",
                "EnvironmentalFireRuntime.TryApplySuppression",
                "EnvironmentalFireSuppressionResult",
                EnvironmentOutcomeIds.FireSuppression,
                new[]
                {
                    "fire.phase", "fire.intensity",
                    "fire.initialAttackApplied"
                },
                new[] { "role:fire-target", "role:fire-responder" },
                new[] { "fire-suppression" }),
            Action(
                "action:environment-fire-fuel-ignition",
                "environment",
                "EnvironmentalFireRuntime.TryIgnite",
                "EnvironmentalFireFuelLossReceipt",
                EnvironmentOutcomeIds.FireFuelLoss,
                new[]
                {
                    "fire.phase", "fire.fireId", "fire.intensity",
                    "fire.remainingFuel", "fire.fuelStackQuantity",
                    "fire.fuelIgnited"
                },
                new[] { "role:fire-target" },
                new[] { "fire-fuel-loss", "fire-ignition" }),
            Action(
                "action:environment-fire-damage-tick",
                "environment",
                "EnvironmentalFireRuntime.Advance",
                "EnvironmentalFireDamageResult",
                EnvironmentOutcomeIds.FireDamage,
                new[]
                {
                    "fire.phase", "fire.intensity", "fire.remainingFuel",
                    "fire.totalDamage", "fire.targetHitPoints",
                    "fire.damageApplied"
                },
                new[] { "role:fire-target" },
                new[] { "fire-damage" }),
            Action(
                "action:environment-fire-water-suppression",
                "environment",
                "EnvironmentalFireRuntime.TryApplySuppression",
                "EnvironmentalFireWaterReceipt",
                EnvironmentOutcomeIds.FireWaterConsumed,
                new[]
                {
                    "fire.phase", "fire.intensity",
                    "fire.totalWaterConsumed", "fire.waterStackQuantity",
                    "fire.waterSuppressed"
                },
                new[] { "role:fire-target", "role:fire-responder" },
                new[] { "fire-water-use", "fire-suppression" }),
            Action(
                RoomEnvironmentCausalProductionEntryDebugScenarios
                    .ExperienceActionId,
                "environment",
                "RoomEnvironmentExperienceService.Apply",
                "RoomEnvironmentExperienceOutcomeReceipt",
                EnvironmentOutcomeIds.RoomExperienceApplied,
                new[]
                {
                    "roomEnvironment.ownerId", "roomEnvironment.actorId",
                    "roomEnvironment.facilityId", "roomEnvironment.roomId",
                    "roomEnvironment.stage",
                    "roomEnvironment.facilityCleanliness",
                    "roomEnvironment.observedCleanliness",
                    "roomEnvironment.evaluatedCleanliness",
                    "roomEnvironment.impressiveness",
                    "roomEnvironment.nextOutcomeSequence",
                    "roomEnvironment.mood",
                    "roomEnvironment.roomMoodFactorCount",
                    "roomEnvironment.exactOutcomeCount",
                    "roomEnvironment.experienceApplied",
                    "roomEnvironment.conditionChanged"
                },
                new[] { "role:room-worker", "role:room-facility", "role:room" },
                new[] { "room-experience", "work-mood" }),
            Action(
                RoomEnvironmentCausalProductionEntryDebugScenarios
                    .ConditionActionId,
                "environment",
                "RoomEnvironmentExperienceService.Apply",
                "RoomConditionOutcomeReceipt",
                EnvironmentOutcomeIds.RoomConditionChanged,
                new[]
                {
                    "roomEnvironment.ownerId", "roomEnvironment.actorId",
                    "roomEnvironment.facilityId", "roomEnvironment.roomId",
                    "roomEnvironment.stage",
                    "roomEnvironment.facilityCleanliness",
                    "roomEnvironment.observedCleanliness",
                    "roomEnvironment.evaluatedCleanliness",
                    "roomEnvironment.impressiveness",
                    "roomEnvironment.nextOutcomeSequence",
                    "roomEnvironment.mood",
                    "roomEnvironment.roomMoodFactorCount",
                    "roomEnvironment.exactOutcomeCount",
                    "roomEnvironment.experienceApplied",
                    "roomEnvironment.conditionChanged"
                },
                new[] { "role:room-worker", "role:room-facility", "role:room" },
                new[] { "room-condition-change", "cleanliness-degradation" }),
            Action(
                EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                    .SpeciesFirstIncidentActionId,
                "environment",
                "CharacterSpeciesRuntime.Tick",
                "SpeciesIncidentOutcomeReceipt",
                EnvironmentOutcomeIds.SpeciesIncidentTriggered,
                new[]
                {
                    "speciesIncident.ownerId", "speciesIncident.actorId",
                    "speciesIncident.facilityId", "speciesIncident.stage",
                    "speciesIncident.speciesId", "speciesIncident.incidentId",
                    "speciesIncident.incidentCount",
                    "speciesIncident.nextIncidentAt", "speciesIncident.charge",
                    "speciesIncident.integrity", "speciesIncident.actorHealth",
                    "speciesIncident.facilityDamaged",
                    "speciesIncident.exactOutcomeCount",
                    "speciesIncident.firstExecuted",
                    "speciesIncident.repeatExecuted"
                },
                new[] { "role:species-character", "role:species-definition" },
                new[] { "species-incident", "golem-core-overload" }),
            Action(
                EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                    .SpeciesRepeatIncidentActionId,
                "environment",
                "CharacterSpeciesRuntime.Tick",
                "SpeciesIncidentOutcomeReceipt",
                EnvironmentOutcomeIds.SpeciesIncidentTriggered,
                new[]
                {
                    "speciesIncident.ownerId", "speciesIncident.actorId",
                    "speciesIncident.facilityId", "speciesIncident.stage",
                    "speciesIncident.speciesId", "speciesIncident.incidentId",
                    "speciesIncident.incidentCount",
                    "speciesIncident.nextIncidentAt", "speciesIncident.charge",
                    "speciesIncident.integrity", "speciesIncident.actorHealth",
                    "speciesIncident.facilityDamaged",
                    "speciesIncident.exactOutcomeCount",
                    "speciesIncident.firstExecuted",
                    "speciesIncident.repeatExecuted"
                },
                new[] { "role:species-character", "role:species-definition" },
                new[] { "species-incident", "golem-core-overload-recurrence" }),
            Action(
                EnvironmentCropProductionEntryDebugScenarios
                    .CertifiedSeedFirstCompletionActionId,
                "environment",
                "CertifiedSeedRuntime.TryPlan/CompleteDeliveredPlans",
                "CertifiedSeedCompletionOutcomeReceipt",
                EnvironmentOutcomeIds.CertifiedSeedCompleted,
                new[]
                {
                    "certifiedSeed.ownerId", "certifiedSeed.facilityId",
                    "certifiedSeed.cropId", "certifiedSeed.stage",
                    "certifiedSeed.nextOrderSequence",
                    "certifiedSeed.lastProcessedOperatingDay",
                    "certifiedSeed.operatingDay",
                    "certifiedSeed.activeOrderCount",
                    "certifiedSeed.certifiedLotCount",
                    "certifiedSeed.exactOutcomeCount",
                    "certifiedSeed.firstExecuted",
                    "certifiedSeed.repeatExecuted"
                },
                new[]
                {
                    "role:certified-seed-facility",
                    "role:certified-seed-output"
                },
                new[] { "certified-seed-completion", "physical-transformation" }),
            Action(
                EnvironmentCropProductionEntryDebugScenarios
                    .CertifiedSeedRepeatCompletionActionId,
                "environment",
                "CertifiedSeedRuntime.TryPlan/CompleteDeliveredPlans",
                "CertifiedSeedCompletionOutcomeReceipt",
                EnvironmentOutcomeIds.CertifiedSeedCompleted,
                new[]
                {
                    "certifiedSeed.ownerId", "certifiedSeed.facilityId",
                    "certifiedSeed.cropId", "certifiedSeed.stage",
                    "certifiedSeed.nextOrderSequence",
                    "certifiedSeed.lastProcessedOperatingDay",
                    "certifiedSeed.operatingDay",
                    "certifiedSeed.activeOrderCount",
                    "certifiedSeed.certifiedLotCount",
                    "certifiedSeed.exactOutcomeCount",
                    "certifiedSeed.firstExecuted",
                    "certifiedSeed.repeatExecuted"
                },
                new[]
                {
                    "role:certified-seed-facility",
                    "role:certified-seed-output"
                },
                new[] { "certified-seed-completion", "repeat-production" }),
            Action(
                SocialLifeCausalProductionEntryDebugScenarios
                    .AssaultConflictActionId,
                "relationship",
                "CombatCommandResultApplier.Apply",
                "SocialConflictOutcomeReceipt",
                SocialLifeOutcomeIds.ConflictResolved,
                new[]
                {
                    "social.ownerId", "social.offenderId",
                    "social.recipientId", "social.recipientHealth",
                    "social.recipientMood", "social.relationshipSentiment",
                    "social.canForgive", "social.identityRuleStateCount",
                    "social.assaultExecuted"
                },
                new[] { "role:social-offender", "role:social-recipient" },
                new[] { "social-conflict", "combat-consequence" }),
            Action(
                SocialLifeCausalProductionEntryDebugScenarios.ApologyActionId,
                "relationship",
                "CharacterApologyCommandRuntime.TryApologize",
                "ApologyOutcomeReceipt",
                SocialLifeOutcomeIds.ApologyResolved,
                new[]
                {
                    "social.ownerId", "social.offenderId",
                    "social.recipientId", "social.recipientHealth",
                    "social.recipientMood", "social.relationshipSentiment",
                    "social.canForgive", "social.identityRuleStateCount",
                    "social.assaultExecuted", "social.apologyExecuted"
                },
                new[] { "role:social-offender", "role:social-recipient" },
                new[] { "social-apology", "relationship-repair" }),
            Action(
                StaffRebellionResponseDebugScenarios.CausalIsolateActionId,
                "relationship",
                "StaffDiscontentRuntime.TryIsolateRebel",
                "StaffDiscontentOutcomeReceipt",
                StaffDiscontentOutcomeIds.ResponseResolved,
                new[]
                {
                    "staffResponse.ownerId", "staffResponse.rebelId",
                    "staffResponse.workerId", "staffResponse.responderId",
                    "staffResponse.stage", "staffResponse.outcomeRevision",
                    "staffResponse.rebelStage", "staffResponse.rebelIsolated",
                    "staffResponse.workerStage", "staffResponse.workerMood",
                    "staffResponse.workerLowMoodDays",
                    "staffResponse.exactOutcomeCount",
                    "staffResponse.isolationExecuted",
                    "staffResponse.calmExecuted"
                },
                new[]
                {
                    "role:staff-rebel", "role:staff-responder"
                },
                new[] { "staff-rebellion-response", "staff-isolation" }),
            Action(
                StaffRebellionResponseDebugScenarios.CausalCalmActionId,
                "relationship",
                "StaffDiscontentRuntime.TryCalmStaff",
                "StaffDiscontentOutcomeReceipt",
                StaffDiscontentOutcomeIds.ResponseResolved,
                new[]
                {
                    "staffResponse.ownerId", "staffResponse.rebelId",
                    "staffResponse.workerId", "staffResponse.responderId",
                    "staffResponse.stage", "staffResponse.outcomeRevision",
                    "staffResponse.rebelStage", "staffResponse.rebelIsolated",
                    "staffResponse.workerStage", "staffResponse.workerMood",
                    "staffResponse.workerLowMoodDays",
                    "staffResponse.exactOutcomeCount",
                    "staffResponse.isolationExecuted",
                    "staffResponse.calmExecuted"
                },
                new[]
                {
                    "role:disruptive-worker", "role:staff-responder"
                },
                new[] { "staff-rebellion-response", "staff-calming" }),
            Action(
                EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                    .FirstSuspensionActionId,
                "work",
                "SettlementAlertRuntime.RecordSuspendedWork",
                "EmergencyWorkSuspensionOutcomeReceipt",
                EmergencyWorkSuspensionOutcomeIds.SuspensionRecorded,
                new[]
                {
                    "emergencySuspension.ownerId",
                    "emergencySuspension.firstWorkerId",
                    "emergencySuspension.secondWorkerId",
                    "emergencySuspension.facilityId",
                    "emergencySuspension.stage",
                    "emergencySuspension.alertEpoch",
                    "emergencySuspension.calendarAbsoluteHour",
                    "emergencySuspension.suspendedWorkCount",
                    "emergencySuspension.firstRecorded",
                    "emergencySuspension.firstCompletedWork",
                    "emergencySuspension.firstRequiredWork",
                    "emergencySuspension.secondRecorded",
                    "emergencySuspension.secondCompletedWork",
                    "emergencySuspension.secondRequiredWork",
                    "emergencySuspension.exactOutcomeCount"
                },
                new[]
                {
                    "role:emergency-first-worker",
                    "role:emergency-work-facility"
                },
                new[]
                {
                    "emergency-work-suspension",
                    "active-assignment-preservation"
                }),
            Action(
                EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                    .SecondSuspensionActionId,
                "work",
                "SettlementAlertRuntime.RecordSuspendedWork",
                "EmergencyWorkSuspensionOutcomeReceipt",
                EmergencyWorkSuspensionOutcomeIds.SuspensionRecorded,
                new[]
                {
                    "emergencySuspension.ownerId",
                    "emergencySuspension.firstWorkerId",
                    "emergencySuspension.secondWorkerId",
                    "emergencySuspension.facilityId",
                    "emergencySuspension.stage",
                    "emergencySuspension.alertEpoch",
                    "emergencySuspension.calendarAbsoluteHour",
                    "emergencySuspension.suspendedWorkCount",
                    "emergencySuspension.firstRecorded",
                    "emergencySuspension.firstCompletedWork",
                    "emergencySuspension.firstRequiredWork",
                    "emergencySuspension.secondRecorded",
                    "emergencySuspension.secondCompletedWork",
                    "emergencySuspension.secondRequiredWork",
                    "emergencySuspension.exactOutcomeCount"
                },
                new[]
                {
                    "role:emergency-second-worker",
                    "role:emergency-work-facility"
                },
                new[]
                {
                    "emergency-work-suspension",
                    "multi-worker-continuity"
                }),
            Action(
                MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                    .FirstAwardActionId,
                "item",
                "MemoryErasureSealBossAwardService.TryAwardForVictory",
                "MemoryErasureBossAwardOutcomeReceipt",
                EvolutionOutcomeIds.MemoryErasureBossAwarded,
                new[]
                {
                    "bossAward.ownerId", "bossAward.firstRegionId",
                    "bossAward.secondRegionId", "bossAward.stage",
                    "bossAward.absoluteDay", "bossAward.awardedRegionCount",
                    "bossAward.physicalPublicationCount",
                    "bossAward.firstAwarded", "bossAward.secondAwarded",
                    "bossAward.firstSealQuantity",
                    "bossAward.secondSealQuantity",
                    "bossAward.totalSealQuantity",
                    "bossAward.exactOutcomeCount"
                },
                new[]
                {
                    "role:first-boss-award-region", "role:boss-award-seal"
                },
                new[]
                {
                    "region-first-boss-award", "physical-item-publication"
                }),
            Action(
                MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                    .SecondAwardActionId,
                "item",
                "MemoryErasureSealBossAwardService.TryAwardForVictory",
                "MemoryErasureBossAwardOutcomeReceipt",
                EvolutionOutcomeIds.MemoryErasureBossAwarded,
                new[]
                {
                    "bossAward.ownerId", "bossAward.firstRegionId",
                    "bossAward.secondRegionId", "bossAward.stage",
                    "bossAward.absoluteDay", "bossAward.awardedRegionCount",
                    "bossAward.physicalPublicationCount",
                    "bossAward.firstAwarded", "bossAward.secondAwarded",
                    "bossAward.firstSealQuantity",
                    "bossAward.secondSealQuantity",
                    "bossAward.totalSealQuantity",
                    "bossAward.exactOutcomeCount"
                },
                new[]
                {
                    "role:second-boss-award-region", "role:boss-award-seal"
                },
                new[]
                {
                    "region-first-boss-award", "multi-region-continuity"
                }),
            Action(
                TradeInventoryCausalProductionEntryDebugScenarios
                    .RelocateActionId,
                "trade-inventory",
                "PhysicalItemRelocationService.TryRelocateQuantity",
                "PhysicalItemRelocationReceipt",
                TradeInventoryOutcomeIds.Result,
                TradeInventoryStateKeys(),
                new[]
                {
                    "role:trade-inventory-source",
                    "role:trade-inventory-destination"
                },
                new[]
                {
                    "physical-item-relocation", "identity-preserving-split"
                }),
            Action(
                TradeInventoryCausalProductionEntryDebugScenarios
                    .DisposeActionId,
                "trade-inventory",
                "PhysicalItemBatchDispositionService.TryCommit",
                "PhysicalItemBatchDispositionReceipt",
                TradeInventoryOutcomeIds.Result,
                TradeInventoryStateKeys(),
                new[]
                {
                    "role:trade-inventory-destination"
                },
                new[]
                {
                    "physical-item-disposition", "source-stack-preservation"
                }),
            Action(
                TradeInventoryCausalProductionEntryDebugScenarios
                    .WastePolicyActionId,
                "trade-inventory",
                "WasteProcessingRuntime.SetPolicy",
                "WastePolicyCommandResult",
                TradeInventoryOutcomeIds.Result,
                TradeInventoryStateKeys(),
                new[]
                {
                    "role:trade-inventory-waste-policy"
                },
                new[]
                {
                    "waste-policy-change", "inventory-operation-continuity"
                }),
            Action(
                DiseaseRouteExposureCausalProductionEntryDebugScenarios
                    .FirstExposureActionId,
                "environment",
                "WildlifeDiseaseVectorRuntime.PublishDailyExposure",
                nameof(PopulationDiseaseRouteExposureEvent),
                EnvironmentOutcomeIds.DiseaseRouteExposure,
                DiseaseRouteStateKeys(),
                new[]
                {
                    "role:disease-route-actor",
                    "role:disease-route-vector",
                    "role:disease-route-disease"
                },
                new[]
                {
                    "wildlife-disease-exposure", "first-observed-contact"
                }),
            Action(
                DiseaseRouteExposureCausalProductionEntryDebugScenarios
                    .RepeatExposureActionId,
                "environment",
                "WildlifeDiseaseVectorRuntime.PublishDailyExposure",
                nameof(PopulationDiseaseRouteExposureEvent),
                EnvironmentOutcomeIds.DiseaseRouteExposure,
                DiseaseRouteStateKeys(),
                new[]
                {
                    "role:disease-route-actor",
                    "role:disease-route-vector",
                    "role:disease-route-disease"
                },
                new[]
                {
                    "wildlife-disease-exposure", "repeated-contact-accumulation"
                }),
            Action(
                OffenseNodeResolutionCausalProductionEntryDebugScenarios
                    .ClearApproachActionId,
                "offense",
                "OffenseExpeditionRuntime.TryResolveCurrentNode",
                MigratedProducerOutcomeKind.OffenseExpeditionNodeResult,
                OffenseNodeResolutionStateKeys(),
                new[] { "role:offense-node-expedition" },
                new[]
                {
                    "expedition-node-resolution", "tool-assisted-event",
                    "authored-cache-entry"
                }),
            Action(
                OffenseNodeResolutionCausalProductionEntryDebugScenarios
                    .SecureCacheActionId,
                "offense",
                "OffenseExpeditionRuntime.TryResolveCurrentNode",
                MigratedProducerOutcomeKind.OffenseExpeditionNodeResult,
                OffenseNodeResolutionStateKeys(),
                new[] { "role:offense-node-expedition" },
                new[]
                {
                    "expedition-node-resolution", "authored-cache",
                    "carried-stock"
                }),
            Action(
                OffenseTruthRevealCausalProductionEntryDebugScenarios
                    .PreliminaryVictoryActionId,
                "offense",
                "OffenseExpeditionResultFinalizer.Finalize",
                MigratedProducerOutcomeKind.OffenseExpeditionResult,
                OffenseTruthStateKeys(),
                new[] { "role:offense-truth-preliminary-expedition" },
                new[]
                {
                    "preliminary-expedition-victory",
                    "truth-route-prerequisite-established"
                }),
            Action(
                OffenseTruthRevealCausalProductionEntryDebugScenarios
                    .RevealTruthActionId,
                "offense",
                "OffenseWorldMapRuntime.TryRecordSuccessfulExpedition",
                nameof(OffenseTruthRevealOutcomeReceipt),
                ExternalFactionOutcomeIds.TruthRevealed,
                OffenseTruthStateKeys(),
                new[] { "role:offense-truth-final-target" },
                new[]
                {
                    "final-truth-target-completion",
                    "campaign-truth-revelation"
                }),
            Action(
                WorkCompletionIdentityCausalProductionEntryDebugScenarios
                    .FirstHarvestActionId,
                "character",
                "WorkIdentityEventAdapter.EnsureApplied",
                nameof(WorkCompletionIdentityOutcomeReceipt),
                WorkCompletionIdentityOutcomeIds.Applied,
                WorkIdentityStateKeys(),
                new[] { "role:work-identity-worker" },
                new[]
                {
                    "first-harvest-identity-reaction",
                    "daily-small-success-established"
                }),
            Action(
                WorkCompletionIdentityCausalProductionEntryDebugScenarios
                    .NextDayHarvestActionId,
                "character",
                "WorkIdentityEventAdapter.EnsureApplied",
                nameof(WorkCompletionIdentityOutcomeReceipt),
                WorkCompletionIdentityOutcomeIds.Applied,
                WorkIdentityStateKeys(),
                new[] { "role:work-identity-worker" },
                new[]
                {
                    "next-day-harvest-identity-reaction",
                    "restored-worker-history-continuity"
                }),
            Action(
                RunVariableCausalProductionEntryDebugScenarios.ActivateActionId,
                "run",
                "RunVariableRuntime.ActivateOperationVariable",
                MigratedProducerOutcomeKind.RunVariableActivatedEvent,
                RunVariableStateKeys(),
                new[] { "role:run-operation-variable" },
                new[]
                {
                    "operation-variable-activation",
                    "temporary-run-condition-established"
                }),
            Action(
                RunVariableCausalProductionEntryDebugScenarios.ExpireActionId,
                "run",
                "RunVariableRuntime.OnTriggerEvent(OperatingDayEndedEvent)",
                MigratedProducerOutcomeKind.RunVariableExpiredEvent,
                RunVariableStateKeys(),
                new[] { "role:run-operation-variable" },
                new[]
                {
                    "operation-variable-expiration",
                    "restored-duration-completion"
                }),
            Action(
                VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.ReceptionActionId,
                "external", "ExteriorActivityRuntime.TickIncidentStates -> InformantExteriorIncidentHandler.Tick",
                MigratedProducerOutcomeKind.ExteriorVisitorReceptionAppliedEvent,
                VisitorCheckoutStateKeys(), new[] { "role:entered-visitor", "role:visitor-checkout-facility" },
                new[] { "visitor-reception", "committed-reception-mood" }),
            Action(
                VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.CheckoutActionId,
                "service", "Shop.Interact -> ShopCustomerInteractionService -> BuildingVisitEventPublisher",
                MigratedProducerOutcomeKind.FacilityCrimeEvent,
                VisitorCheckoutStateKeys(), new[] { "role:entered-visitor", "role:visitor-checkout-facility" },
                new[] { "controlled-risk-live-checkout", "post-commit-visit-continuation" },
                new[] { MigratedProducerOutcomeKind.FacilityVisitEvent },
                new[] { "post-commit-continuation" }),
            Action(
                TabooButcheryCausalProductionEntryDebugScenarios.ControlledDeathActionId,
                "survival", "CharacterActor.Die -> CharacterBodyHealthRuntime.Kill -> CharacterDeprivationRuntime.OnCharacterDeath",
                MigratedProducerOutcomeKind.CharacterDeathEvent,
                TabooButcheryStateKeys(),
                new[] { "role:taboo-victim", "role:taboo-corpse", "role:taboo-witness" },
                new[] { "controlled-death", "corpse-observer-state", "permission-without-outcome" },
                new[] { MigratedProducerOutcomeKind.HealthThresholdCrossedEvent },
                new[] { "atomic-sibling" }),
            Action(
                TabooButcheryCausalProductionEntryDebugScenarios.ButcherCorpseActionId,
                "survival", "WildlifeCarcassService.TryButcherNextCarcass -> PhysicalItemTransformService.TryTransformWholeStackWithCompanion",
                MigratedProducerOutcomeKind.PhysicalItemTransformReceipt,
                TabooButcheryStateKeys(),
                new[] { "role:taboo-butcher", "role:taboo-victim", "role:taboo-witness", "role:taboo-corpse" },
                new[] { "restored-corpse-butchery", "physical-output", "taboo-consequence" },
                new[] { MigratedProducerOutcomeKind.CharacterTabooIncidentEvent },
                new[] { "atomic-sibling" }),
            Action(
                OffenseArrivalCausalProductionEntryDebugScenarios.FinalizeRewardActionId,
                "offense",
                "OffenseExpeditionResultFinalizer.Finalize -> OffenseRewardRuntime.ApplyExpeditionRewards -> OffenseSpecialMonsterRewardGrantHandler",
                MigratedProducerOutcomeKind.OffenseExpeditionResult,
                OffenseArrivalStateKeys(),
                new[] { "role:expedition", "role:special-wildlife-reward" },
                new[] { "authored-rival-dungeon-reward", "pending-rune-deer-delivery", "dropoff-unavailable" }),
            Action(
                OffenseArrivalCausalProductionEntryDebugScenarios.SettleArrivalActionId,
                "offense",
                "OffenseReturnArrivalRuntime.Tick -> TryMaterialize -> ResolveUncontainedEscapes -> TryFinalizeResolvedArrival",
                MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt,
                OffenseArrivalStateKeys(),
                new[] { "role:expedition", "role:special-wildlife-reward", "role:arrival-wildlife" },
                new[] { "restored-rune-deer-delivery", "physical-wildlife-materialization", "escaped-before-containment" }),
            Action(
                FestivalCelebrationCausalProductionEntryDebugScenarios.FirstCelebrationActionId,
                "society",
                "FestivalExecutionRuntime.Schedule -> AdvancePreparation -> TryStartFestival -> AdvanceAttendance -> ResolveOccurrence",
                MigratedProducerOutcomeKind.FestivalCelebratedEvent,
                FestivalCelebrationStateKeys(),
                new[] { "role:festival-occurrence", "role:festival-participant", "role:memorial-venue" },
                new[] { "authored-long-night-memorial", "controlled-candle-placement", "actual-candle-consumption", "actual-attendance-effects" }),
            Action(
                FestivalCelebrationCausalProductionEntryDebugScenarios.SecondCelebrationActionId,
                "society",
                "FestivalExecutionSaveSection.StageRestore -> Schedule -> AdvancePreparation -> TryStartFestival -> AdvanceAttendance -> ResolveOccurrence",
                MigratedProducerOutcomeKind.FestivalCelebratedEvent,
                FestivalCelebrationStateKeys(),
                new[] { "role:festival-occurrence", "role:festival-participant", "role:memorial-venue" },
                new[] { "restored-festival-owner", "next-year-long-night-memorial", "actual-candle-consumption", "cumulative-social-effects" }),
            Action(
                OwnerRunCompletionCausalProductionEntryDebugScenarios.CompleteActionId,
                "run", "OwnerRunManager.CompleteRun -> MetaRuntimeApplicationAdapter -> MetaProgressionRuntime.EndRun",
                MigratedProducerOutcomeKind.OwnerRunEndedEvent,
                OwnerRunCompletionStateKeys(),
                new[] { "role:owner-run-character", "role:owner-run-operation" },
                new[] { "owner-run-ended", "post-commit-legacy-settlement" },
                new[] { MigratedProducerOutcomeKind.RunResultReadyEvent },
                new[] { "post-commit-observer" }),
            Action(
                OwnerRunCompletionCausalProductionEntryDebugScenarios.UpgradeActionId,
                "meta", "MetaProgressionRuntime.TryPurchaseUpgrade",
                MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent,
                OwnerRunCompletionStateKeys(), new[] { "role:owner-run-operation" },
                new[] { "authored-meta-upgrade", "restored-owner-run-reward-spent" }),
            Action(
                MetaProgressionCausalProductionEntryDebugScenarios
                    .RunResultActionId,
                "run",
                "MetaProgressionRuntime.EndRun",
                MigratedProducerOutcomeKind.RunResultReadyEvent,
                MetaProgressionStateKeys(),
                new[] { "role:meta-run-progression" },
                new[]
                {
                    "run-result-finalization",
                    "legacy-currency-earned"
                }),
            Action(
                MetaProgressionCausalProductionEntryDebugScenarios
                    .UpgradeActionId,
                "meta",
                "MetaProgressionRuntime.TryPurchaseUpgrade",
                MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent,
                MetaProgressionStateKeys(),
                new[] { "role:meta-run-progression" },
                new[]
                {
                    "meta-upgrade-purchase",
                    "restored-run-reward-spent"
                }),
            Action(
                RegularCustomerCausalProductionEntryDebugScenarios
                    .FirstVisitActionId,
                "service",
                "RegularCustomerRuntime.OnTriggerEvent(FacilityVisitEvent)",
                MigratedProducerOutcomeKind.RegularCustomerVisitResult,
                RegularCustomerStateKeys(),
                new[] { "role:regular-customer" },
                new[]
                {
                    "first-customer-visit",
                    "customer-familiarity-established"
                }),
            Action(
                RegularCustomerCausalProductionEntryDebugScenarios
                    .CandidateVisitActionId,
                "service",
                "RegularCustomerRuntime.OnTriggerEvent(FacilityVisitEvent)",
                MigratedProducerOutcomeKind.RegularCustomerVisitResult,
                RegularCustomerStateKeys(),
                new[] { "role:regular-customer" },
                new[]
                {
                    "repeat-customer-visit",
                    "recruit-candidate-qualified"
                }),
            Action(
                RegularCustomerCausalProductionEntryDebugScenarios
                    .RecruitActionId,
                "service",
                "RegularCustomerRuntime.TryRecruit",
                MigratedProducerOutcomeKind.RegularCustomerRecruitResult,
                RegularCustomerStateKeys(),
                new[] { "role:regular-customer" },
                new[]
                {
                    "regular-customer-recruitment",
                    "restored-visit-history-converted"
                }),
            Action(
                CharacterLifeCausalProductionEntryDebugScenarios
                    .AgeConditionActionId,
                "life",
                "CharacterLifeApplicationAdapter.OnDayEnded(OperatingDayEndedEvent)",
                MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent,
                CharacterLifeStateKeys(),
                new[] { "role:character-life" },
                new[]
                {
                    "age-condition-progression",
                    "daily-aging-state-transition"
                }),
            Action(
                CharacterLifeCausalProductionEntryDebugScenarios
                    .LifeStageActionId,
                "life",
                "CharacterLifeApplicationAdapter.OnDayEnded(OperatingDayEndedEvent)",
                MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent,
                CharacterLifeStateKeys(),
                new[] { "role:character-life" },
                new[]
                {
                    "life-stage-transition",
                    "restored-aging-history-continuity"
                }),
            Action(
                FactionRouteCausalProductionEntryDebugScenarios
                    .SettlementActionId,
                "faction",
                "FactionRuntimeApplicationAdapter.TryRequestTrade",
                MigratedProducerOutcomeKind.FactionRouteSettlementReceipt,
                FactionRouteStateKeys(),
                new[] { "role:faction" },
                new[]
                {
                    "faction-route-settlement",
                    "trade-route-created"
                }),
            Action(
                FactionRouteCausalProductionEntryDebugScenarios
                    .ArrivalActionId,
                "faction",
                "FactionRuntimeApplicationAdapter.Tick(route-arrival)",
                MigratedProducerOutcomeKind.FactionRouteArrivedEvent,
                FactionRouteStateKeys(),
                new[] { "role:faction" },
                new[]
                {
                    "faction-route-arrival",
                    "restored-paid-route-continuity"
                }),
            Action(
                FactionRouteCausalProductionEntryDebugScenarios
                    .CargoActionId,
                "faction",
                "FactionRuntimeApplicationAdapter.Tick(cargo-delivery-retry)",
                MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt,
                FactionRouteStateKeys(),
                new[] { "role:faction" },
                new[]
                {
                    "faction-route-cargo-delivery",
                    "restored-arrival-cargo-continuity"
                }),
            Action(
                ConsumablesProgressionCausalProductionEntryDebugScenarios
                    .MealActionId,
                "survival",
                "CharacterConsumablesRuntime.TryConsumeFieldMeal",
                MigratedProducerOutcomeKind.CharacterConsumablesMealResult,
                ConsumablesStateKeys(),
                new[] { "role:treatment-subject" },
                new[]
                {
                    "field-meal-consumption",
                    "hunger-recovery"
                }),
            Action(
                ConsumablesProgressionCausalProductionEntryDebugScenarios
                    .SubstanceActionId,
                "survival",
                "CharacterConsumablesRuntime.TryConsumeSubstance",
                MigratedProducerOutcomeKind
                    .CharacterConsumablesSubstanceResult,
                ConsumablesStateKeys(),
                new[] { "role:treatment-subject" },
                new[]
                {
                    "medical-substance-consumption",
                    "restored-meal-effect-continuity"
                }),
            Action(
                MealShortageCausalProductionEntryDebugScenarios
                    .FirstMissActionId,
                "survival",
                "SurvivalFoodRuntime.OnTriggerEvent(OperatingDayStartedEvent:2)",
                MigratedProducerOutcomeKind.MealMissedEvent,
                MealShortageStateKeys(),
                new[] { "role:treatment-subject" },
                new[] { "meal-shortage", "first-missed-day" }),
            Action(
                MealShortageCausalProductionEntryDebugScenarios
                    .RepeatMissActionId,
                "survival",
                "SurvivalFoodRuntime.OnTriggerEvent(OperatingDayStartedEvent:3)",
                MigratedProducerOutcomeKind.MealMissedEvent,
                MealShortageStateKeys(),
                new[] { "role:treatment-subject" },
                new[] { "meal-shortage", "restored-next-day-shortage" }),
            Action(
                PrimitiveSurvivalCausalProductionEntryDebugScenarios
                    .FirstRestActionId,
                "survival",
                "CharacterPrimitiveSurvivalRunner.TryStart(FloorRest:day14)",
                MigratedProducerOutcomeKind
                    .CharacterPrimitiveSurvivalCompletedEvent,
                PrimitiveSurvivalStateKeys(),
                new[] { "role:treatment-subject" },
                new[] { "primitive-floor-rest", "first-rest" }),
            Action(
                PrimitiveSurvivalCausalProductionEntryDebugScenarios
                    .RepeatRestActionId,
                "survival",
                "CharacterPrimitiveSurvivalRunner.TryStart(FloorRest:day15)",
                MigratedProducerOutcomeKind
                    .CharacterPrimitiveSurvivalCompletedEvent,
                PrimitiveSurvivalStateKeys(),
                new[] { "role:treatment-subject" },
                new[] { "primitive-floor-rest", "restored-body-state" }),
            Action(
                ServiceHubDayCausalProductionEntryDebugScenarios.VisitActionId,
                "service",
                "BuildingVisitEventPublisher.CommitVisit",
                MigratedProducerOutcomeKind.FacilityVisitEvent,
                ServiceHubDayStateKeys(),
                new[] { "role:service-hub", "role:service-visitor" },
                new[] { "service-facility-visit", "operating-day-visit-ledger" }),
            Action(
                ServiceHubDayCausalProductionEntryDebugScenarios.ModeActionId,
                "service",
                "ServiceSessionRuntime.SwitchToDirect",
                MigratedProducerOutcomeKind.ServiceModeChangeResult,
                ServiceHubDayStateKeys(),
                new[] { "role:service-hub" },
                new[] { "service-mode-change", "restored-visit-continuity" }),
            Action(
                ServiceHubDayCausalProductionEntryDebugScenarios.CloseDayActionId,
                "service",
                "OperatingDaySettlementApplicationAdapter.OnTriggerEvent(OperatingDayEndedEvent)",
                MigratedProducerOutcomeKind.OperatingDayReportEvent,
                ServiceHubDayStateKeys(),
                new[] { "role:service-hub", "role:service-day-operation" },
                new[] { "operating-day-settlement", "retained-visit-report" }),
            Action(
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .InvasionStartActionId,
                "invasion",
                "InvasionDirectorRuntime.TrySpawnIntruder",
                MigratedProducerOutcomeKind.InvasionStartedEvent,
                InvasionLifecycleStateKeys(),
                new[] { "role:invasion-intruder" },
                new[] { "invasion-start", "persistent-intruder-owner" }),
            Action(
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .InvasionBreachActionId,
                "invasion",
                "InvasionIntruderExecutionCoordinator.MarkDungeonBreached",
                MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent,
                InvasionLifecycleStateKeys(),
                new[] { "role:invasion-intruder" },
                new[] { "dungeon-breach", "restored-intruder-owner" }),
            Action(
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .InvasionDamageActionId,
                "invasion",
                "InvasionIntruderRuntime.TryDamageNearbyFacility",
                MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent,
                InvasionLifecycleStateKeys(),
                new[]
                {
                    "role:invasion-intruder",
                    "role:invasion-damage-facility"
                },
                new[] { "facility-damage", "restored-breach-continuity" }),
            Action(
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .InvasionFrontCollapseActionId,
                "invasion",
                "DefenseEngagementCombatRuntime.CollapseFront",
                MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent,
                InvasionLifecycleStateKeys(),
                new[] { "role:invasion-intruder", "role:defense-front" },
                new[] { "defense-front-collapse", "restored-facility-damage" }),
            Action(
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .InvasionResolveActionId,
                "invasion",
                "InvasionIntruderRuntime.ResolveDefenseFailed",
                MigratedProducerOutcomeKind.InvasionResolvedEvent,
                InvasionLifecycleStateKeys(),
                new[] { "role:invasion-intruder", "role:invasion-owner" },
                new[] { "defense-failed", "terminal-invasion-resolution" }),
            Action(
                AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                    .ManifestActionId,
                "character-growth",
                "CharacterAcquiredTraitInferenceService.CompleteMilestone",
                "AcquiredTraitInferenceOutcomeReceipt",
                EvolutionOutcomeIds.TraitInferenceCompleted,
                new[]
                {
                    "trait.ownerId", "trait.characterId", "trait.revision",
                    "trait.activeCount", "trait.narrativeFactCount",
                    "trait.sealQuantity", "trait.manifested",
                    "trait.reactionExecuted", "trait.reactionTriggerCount",
                    "trait.erased", "trait.instanceId", "trait.level",
                    "trait.currentExperience"
                },
                new[] { "role:trait-character" },
                new[] { "acquired-trait-manifestation", "character-growth" }),
            Action(
                AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                    .ReactionActionId,
                "character-growth",
                "CharacterAcquiredTraitSpecialReactionRuntime.OnWorkCompleted",
                "AcquiredTraitReactionOutcomeReceipt",
                EvolutionOutcomeIds.TraitReactionExecuted,
                new[]
                {
                    "trait.ownerId", "trait.characterId", "trait.revision",
                    "trait.activeCount", "trait.narrativeFactCount",
                    "trait.sealQuantity", "trait.manifested",
                    "trait.reactionExecuted", "trait.reactionTriggerCount",
                    "trait.erased", "trait.instanceId", "trait.level",
                    "trait.currentExperience"
                },
                new[] { "role:trait-character" },
                new[] { "acquired-trait-reaction", "work-completion" }),
            Action(
                AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                    .EraseActionId,
                "character-growth",
                "MemoryErasureSealTransactionService.TryErase",
                "MemoryErasureOutcomeReceipt",
                EvolutionOutcomeIds.MemoryErasureTerminal,
                new[]
                {
                    "trait.ownerId", "trait.characterId", "trait.revision",
                    "trait.activeCount", "trait.narrativeFactCount",
                    "trait.sealQuantity", "trait.manifested",
                    "trait.reactionExecuted", "trait.reactionTriggerCount",
                    "trait.erased", "trait.instanceId", "trait.level",
                    "trait.currentExperience"
                },
                new[] { "role:trait-character", "role:memory-erasure-seal" },
                new[] { "memory-erasure", "acquired-trait-removal" }),
            Action(
                CaptivityCausalProductionEntryDebugScenarios.InteractionActionId,
                "captivity",
                "CaptivityInteractionOutcomeRuntime.TryPrepare",
                "CaptivityInteractionOutcomeReceipt",
                CaptivityInteractionOutcomeIds.Resolved,
                CaptivityStateKeys(),
                new[]
                {
                    "role:interaction-captive", "role:interaction-warden",
                    "role:interaction-facility"
                },
                new[] { "captivity-interaction", "persuasion" }),
            Action(
                CaptivityCausalProductionEntryDebugScenarios.MilestoneActionId,
                "captivity",
                "CaptivePerformerMilestoneOutcomeRuntime.TryCommitEligibleMilestones",
                "CaptivePerformerMilestoneOutcomeReceipt",
                CaptivePerformerMilestoneOutcomeIds.Unlocked,
                CaptivityStateKeys(),
                new[] { "role:captive-performer" },
                new[] { "captive-performer", "milestone-unlock" }),
            Action(
                CaptivityCausalProductionEntryDebugScenarios.RansomActionId,
                "captivity",
                "CaptiveRansomOutcomeRuntime.TryPrepare",
                "CaptiveRansomOutcomeReceipt",
                CaptiveRansomOutcomeIds.Ransomed,
                CaptivityStateKeys(),
                new[] { "role:ransomed-captive" },
                new[] { "captive-ransom", "captivity-terminal" }),
            Action(
                CaptivityCausalProductionEntryDebugScenarios.EscapeActionId,
                "captivity",
                "CaptiveEscapeOutcomeRuntime.TryPrepare",
                "CaptiveEscapeOutcomeReceipt",
                CaptiveEscapeOutcomeIds.Escaped,
                CaptivityStateKeys(),
                new[] { "role:escaped-captive" },
                new[] { "captive-escape", "betrayal", "captivity-terminal" }),
            Action(
                EquipmentApparelCausalProductionEntryDebugScenarios.CraftActionId,
                "equipment",
                "ApparelPhysicalTransaction.ExecuteCraftOrResume",
                "ApparelPhysicalOutcomeReceipt",
                EvolutionOutcomeIds.ApparelPhysicalCompleted,
                new[]
                {
                    "equipment.ownerId", "equipment.characterId",
                    "equipment.facilityId", "equipment.apparelDefinitionId",
                    "equipment.materialQuantity", "equipment.outputQuantity",
                    "equipment.outputStackId", "equipment.outputInstanceId",
                    "equipment.outputState", "equipment.apparelDurability",
                    "equipment.crafted", "equipment.equipped"
                },
                new[]
                {
                    "role:apparel-maker", "role:apparel-facility",
                    "role:apparel-item"
                },
                new[] { "equipment-crafting", "equipment-acquisition" }),
            Action(
                EquipmentApparelCausalProductionEntryDebugScenarios.EquipActionId,
                "equipment",
                "CharacterApparelAggregate.TryCommitChange",
                "ApparelChangeOutcomeReceipt",
                EvolutionOutcomeIds.ApparelChanged,
                new[]
                {
                    "equipment.ownerId", "equipment.characterId",
                    "equipment.apparelDefinitionId", "equipment.outputQuantity",
                    "equipment.outputStackId", "equipment.outputInstanceId",
                    "equipment.outputState", "equipment.apparelDurability",
                    "equipment.apparelVersion", "equipment.equippedCount",
                    "equipment.crafted", "equipment.equipped"
                },
                new[] { "role:apparel-wearer", "role:apparel-item" },
                new[] { "equipment-equip", "equipment-acquisition" }),
            Action(
                KnowledgeResearchOutcomeDebugScenarios
                    .CausalResearchProgressActionId,
                "research",
                "KnowledgeResidueProcessingRuntime.ApplyApprovedWork",
                "ResearchWorkOutcomeReceipt",
                ResearchWorkOutcomeIds.WorkApplied,
                new[]
                {
                    "research.ownerId", "research.facilityId",
                    "research.taskId", "research.sourceStackId",
                    "research.completedWork", "research.requiredWork",
                    "research.intelligenceDamage",
                    "research.outcomeSequence",
                    "research.sourceAvailable", "research.progressApplied",
                    "research.completed"
                },
                new[] { "role:research-task", "role:research-facility" },
                new[] { "research-progress", "knowledge-residue" }),
            Action(
                KnowledgeResearchOutcomeDebugScenarios
                    .CausalResearchCompletionActionId,
                "research",
                "KnowledgeResidueProcessingRuntime.ApplyApprovedWork",
                "ResearchWorkOutcomeReceipt",
                ResearchWorkOutcomeIds.WorkApplied,
                new[]
                {
                    "research.ownerId", "research.facilityId",
                    "research.taskId", "research.sourceStackId",
                    "research.completedWork", "research.requiredWork",
                    "research.intelligenceDamage",
                    "research.outcomeSequence",
                    "research.sourceAvailable", "research.progressApplied",
                    "research.completed"
                },
                new[] { "role:research-task", "role:research-facility" },
                new[] { "research-completion", "knowledge-residue" }),
            Action(
                ProductionPreparedOutputFullPersistenceDebugScenarios
                    .CausalProductionCommandActionId,
                "production",
                "ProductionBillRuntime.AddBill",
                "ProductionCommandOutcomeReceipt",
                ProductionCommandOutcomeIds.Applied,
                new[]
                {
                    "production.ownerId", "production.facilityId",
                    "production.workerId", "production.recipeId",
                    "production.billId", "production.billCount",
                    "production.commandOutcomeSequence",
                    "production.cycleSequence", "production.remainingCycles",
                    "production.completedWork", "production.preparedOutputPhase",
                    "production.outputQuantity", "production.outputMassGrams",
                    "production.commandApplied", "production.completed"
                },
                new[] { "role:production-facility" },
                new[] { "production-command", "production-order" }),
            Action(
                ProductionPreparedOutputFullPersistenceDebugScenarios
                    .CausalProductionCompletionActionId,
                "production",
                "ProductionBillRuntime.ExecuteWork",
                "ProductionCompletedOutcomeReceipt",
                ProductionCombatOutcomeIds.ProductionCompleted,
                new[]
                {
                    "production.ownerId", "production.facilityId",
                    "production.workerId", "production.recipeId",
                    "production.billId", "production.billCount",
                    "production.commandOutcomeSequence",
                    "production.cycleSequence", "production.remainingCycles",
                    "production.completedWork", "production.preparedOutputPhase",
                    "production.outputQuantity", "production.outputMassGrams",
                    "production.commandApplied", "production.completed"
                },
                new[] { "role:production-facility", "role:production-worker" },
                new[] { "production-completion", "physical-output" }),
            Action(
                FacilityEvolutionDebugScenarios
                    .CausalFacilityEvolutionFirstActionId,
                "facility",
                "FacilityEvolutionEngine.TryCommitFormulaPresentation",
                "FacilityEvolutionResult",
                EvolutionOutcomeIds.FacilityEvolutionCompleted,
                new[]
                {
                    "facility.ownerId", "facility.instanceId",
                    "facility.definitionId", "facility.displayName",
                    "facility.starGrade", "facility.historyCount",
                    "facility.generation", "facility.mercenaryTokenCount",
                    "facility.mutationTagCount", "facility.exactOutcomeCount",
                    "facility.evolutionStage", "facility.firstCompleted",
                    "facility.secondCompleted"
                },
                new[] { "role:facility-evolution-target" },
                new[] { "facility-evolution", "facility-lineage" }),
            Action(
                FacilityEvolutionDebugScenarios
                    .CausalFacilityEvolutionSecondActionId,
                "facility",
                "FacilityEvolutionEngine.TryCommitFormulaPresentation",
                "FacilityEvolutionResult",
                EvolutionOutcomeIds.FacilityEvolutionCompleted,
                new[]
                {
                    "facility.ownerId", "facility.instanceId",
                    "facility.definitionId", "facility.displayName",
                    "facility.starGrade", "facility.historyCount",
                    "facility.generation", "facility.mercenaryTokenCount",
                    "facility.mutationTagCount", "facility.exactOutcomeCount",
                    "facility.evolutionStage", "facility.firstCompleted",
                    "facility.secondCompleted"
                },
                new[] { "role:facility-evolution-target" },
                new[] { "facility-evolution", "facility-lineage" }),
            Action(
                FacilitySynthesisDebugScenarios
                    .CausalSynthesisFirstActionId,
                "facility",
                "FacilitySynthesisRuntime.TrySynthesize",
                "FacilitySynthesisResult",
                FacilitySynthesisOutcomeIds.Completed,
                new[]
                {
                    "facilitySynthesis.ownerId", "facilitySynthesis.stage",
                    "facilitySynthesis.survivorId",
                    "facilitySynthesis.definitionId",
                    "facilitySynthesis.displayName",
                    "facilitySynthesis.facilityLevel",
                    "facilitySynthesis.outcomeRevision",
                    "facilitySynthesis.liveBuildingCount",
                    "facilitySynthesis.exactOutcomeCount",
                    "facilitySynthesis.battleBannerUnlocked",
                    "facilitySynthesis.patrolBoardCompleted",
                    "facilitySynthesis.battleBannerCompleted"
                },
                new[]
                {
                    "role:facility-synthesis-target",
                    "role:facility-synthesis-material"
                },
                new[] { "facility-synthesis", "patrol-board" }),
            Action(
                FacilitySynthesisDebugScenarios
                    .CausalSynthesisSecondActionId,
                "facility",
                "FacilitySynthesisRuntime.TrySynthesize",
                "FacilitySynthesisResult",
                FacilitySynthesisOutcomeIds.Completed,
                new[]
                {
                    "facilitySynthesis.ownerId", "facilitySynthesis.stage",
                    "facilitySynthesis.survivorId",
                    "facilitySynthesis.definitionId",
                    "facilitySynthesis.displayName",
                    "facilitySynthesis.facilityLevel",
                    "facilitySynthesis.outcomeRevision",
                    "facilitySynthesis.liveBuildingCount",
                    "facilitySynthesis.exactOutcomeCount",
                    "facilitySynthesis.battleBannerUnlocked",
                    "facilitySynthesis.patrolBoardCompleted",
                    "facilitySynthesis.battleBannerCompleted"
                },
                new[]
                {
                    "role:facility-synthesis-target",
                    "role:facility-synthesis-material"
                },
                new[] { "facility-synthesis", "battle-banner" }),
            Action(
                InfrastructureCommandCausalProductionEntryDebugScenarios
                    .DisconnectActionId,
                "infrastructure",
                "ElectricalNetworkRuntime.SetConnectionEnabled",
                "InfrastructureCommandOutcomeReceipt",
                InfrastructureCommandOutcomeIds.Applied,
                new[]
                {
                    "infrastructure.ownerId", "infrastructure.facilityId",
                    "infrastructure.displayName",
                    "infrastructure.connectionEnabled",
                    "infrastructure.powerNodeCount",
                    "infrastructure.commandNextOwnerRevision",
                    "infrastructure.pendingOutcomeCount",
                    "infrastructure.exactOutcomeCount",
                    "infrastructure.stage", "infrastructure.disconnected",
                    "infrastructure.reconnected"
                },
                new[] { "role:infrastructure-facility" },
                new[] { "power-disconnection", "infrastructure-command" }),
            Action(
                InfrastructureCommandCausalProductionEntryDebugScenarios
                    .ReconnectActionId,
                "infrastructure",
                "ElectricalNetworkRuntime.SetConnectionEnabled",
                "InfrastructureCommandOutcomeReceipt",
                InfrastructureCommandOutcomeIds.Applied,
                new[]
                {
                    "infrastructure.ownerId", "infrastructure.facilityId",
                    "infrastructure.displayName",
                    "infrastructure.connectionEnabled",
                    "infrastructure.powerNodeCount",
                    "infrastructure.commandNextOwnerRevision",
                    "infrastructure.pendingOutcomeCount",
                    "infrastructure.exactOutcomeCount",
                    "infrastructure.stage", "infrastructure.disconnected",
                    "infrastructure.reconnected"
                },
                new[] { "role:infrastructure-facility" },
                new[] { "power-reconnection", "infrastructure-command" }),
            Action(
                DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                    .QuarryActionId,
                "infrastructure",
                "GameEventBus.Publish<BlueprintResearchCompletedEvent>",
                "DungeonSpaceExpansionOutcomeReceipt",
                DungeonSpaceExpansionOutcomeIds.Expanded,
                new[]
                {
                    "dungeonSpace.ownerId", "dungeonSpace.stage",
                    "dungeonSpace.tier", "dungeonSpace.interiorColumns",
                    "dungeonSpace.gridWidth", "dungeonSpace.entranceX",
                    "dungeonSpace.entranceY",
                    "dungeonSpace.exactOutcomeCount",
                    "dungeonSpace.quarryCompleted",
                    "dungeonSpace.stonecuttingCompleted"
                },
                new[] { "role:dungeon-space", "role:unlocking-research" },
                new[] { "dungeon-space-expansion", "quarry-research" }),
            Action(
                DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                    .StonecuttingActionId,
                "infrastructure",
                "GameEventBus.Publish<BlueprintResearchCompletedEvent>",
                "DungeonSpaceExpansionOutcomeReceipt",
                DungeonSpaceExpansionOutcomeIds.Expanded,
                new[]
                {
                    "dungeonSpace.ownerId", "dungeonSpace.stage",
                    "dungeonSpace.tier", "dungeonSpace.interiorColumns",
                    "dungeonSpace.gridWidth", "dungeonSpace.entranceX",
                    "dungeonSpace.entranceY",
                    "dungeonSpace.exactOutcomeCount",
                    "dungeonSpace.quarryCompleted",
                    "dungeonSpace.stonecuttingCompleted"
                },
                new[] { "role:dungeon-space", "role:unlocking-research" },
                new[] { "dungeon-space-expansion", "stonecutting-research" })
        },
        ["scenarioTemplates"] = new JArray
        {
            Template(
                "protagonist-injury-recovery-growth",
                "single-protagonist-with-explicit-enemy",
                new[]
                {
                    "action:receive-nonlethal-combat-injury",
                    "action:recover-body-health",
                    "action:drink-clean-water",
                    "action:rest",
                    "action:train-proficiency"
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "actor.thirst", "less-or-equal", 20),
                    Predicate("initial", "actor.sleep", "less-or-equal", 20)
                },
                new[]
                {
                    Edge("action:receive-nonlethal-combat-injury", "action:recover-body-health",
                        "observed-state", "health.downed", "health.currentHealth"),
                    Edge("action:drink-clean-water", "action:rest", "committed-outcome"),
                    Edge("action:rest", "action:train-proficiency", "observed-state", "actor.sleep")
                }),
            Template(
                "combat-life-cycle-sequence",
                "single-persistent-combat-life-cycle-owner",
                new[]
                {
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .NonlethalCombatInjuryActionId,
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .RecoverBodyHealthActionId,
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .FatalCombatInjuryActionId
                },
                Array.Empty<string>(),
                CombatLifeCycleInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        MigratedProducerCombatDefenseEntryE2EDebugScenarios
                            .NonlethalCombatInjuryActionId,
                        MigratedProducerCombatDefenseEntryE2EDebugScenarios
                            .RecoverBodyHealthActionId,
                        "observed-state",
                        CombatLifeCycleStateKeys()),
                    Edge(
                        MigratedProducerCombatDefenseEntryE2EDebugScenarios
                            .RecoverBodyHealthActionId,
                        MigratedProducerCombatDefenseEntryE2EDebugScenarios
                            .FatalCombatInjuryActionId,
                        "observed-state",
                        CombatLifeCycleStateKeys())
                }),
            Template(
                "combat-life-cycle-injury-episode",
                "single-persistent-combat-life-cycle-owner",
                new[]
                {
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .NonlethalCombatInjuryActionId
                },
                Array.Empty<string>(),
                CombatLifeCycleInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "combat-life-cycle-recovery-episode",
                "single-persistent-combat-life-cycle-owner",
                new[]
                {
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .RecoverBodyHealthActionId
                },
                Array.Empty<string>(),
                CombatLifeCycleInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "combat-life-cycle-fatal-episode",
                "single-persistent-combat-life-cycle-owner",
                new[]
                {
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .FatalCombatInjuryActionId
                },
                Array.Empty<string>(),
                CombatLifeCycleInitialPredicates(stage: 2),
                Array.Empty<JObject>()),
            Template(
                "stock-supply-transform-sequence",
                "single-persistent-stock-supply-transform-owner",
                new[]
                {
                    StockSupplyTransformCausalProductionEntryDebugScenarios
                        .SupplyActionId,
                    StockSupplyTransformCausalProductionEntryDebugScenarios
                        .TransformActionId
                },
                Array.Empty<string>(),
                StockSupplyTransformInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        StockSupplyTransformCausalProductionEntryDebugScenarios
                            .SupplyActionId,
                        StockSupplyTransformCausalProductionEntryDebugScenarios
                            .TransformActionId,
                        "observed-state",
                        StockSupplyTransformStateKeys())
                }),
            Template(
                "stock-supply-purchase-episode",
                "single-persistent-stock-supply-transform-owner",
                new[]
                {
                    StockSupplyTransformCausalProductionEntryDebugScenarios
                        .SupplyActionId
                },
                Array.Empty<string>(),
                StockSupplyTransformInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "stock-supply-transform-episode",
                "single-persistent-stock-supply-transform-owner",
                new[]
                {
                    StockSupplyTransformCausalProductionEntryDebugScenarios
                        .TransformActionId
                },
                Array.Empty<string>(),
                StockSupplyTransformInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "facility-wishlist-procurement-sequence",
                "single-persistent-facility-wishlist-procurement-owner",
                new[]
                {
                    FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                        .BudgetBlockedActionId,
                    FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                        .PurchaseWishlistActionId
                },
                Array.Empty<string>(),
                FacilityWishlistProcurementInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                            .BudgetBlockedActionId,
                        FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                            .PurchaseWishlistActionId,
                        "observed-state",
                        FacilityWishlistProcurementStateKeys())
                }),
            Template(
                "facility-wishlist-budget-blocked-episode",
                "single-persistent-facility-wishlist-procurement-owner",
                new[]
                {
                    FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                        .BudgetBlockedActionId
                },
                Array.Empty<string>(),
                FacilityWishlistProcurementInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "facility-wishlist-purchase-episode",
                "single-persistent-facility-wishlist-procurement-owner",
                new[]
                {
                    FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                        .PurchaseWishlistActionId
                },
                Array.Empty<string>(),
                FacilityWishlistProcurementInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "observed-career-arc-sequence",
                "single-persistent-observed-career-arc-owner",
                new[]
                {
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .MentorshipAwardActionId,
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .LastLessonActionId,
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .QuietPromotionActionId,
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .FuneralActionId,
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .LineageCompressionActionId
                },
                Array.Empty<string>(),
                ObservedCareerArcInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        ObservedCareerArcCausalProductionEntryDebugScenarios
                            .MentorshipAwardActionId,
                        ObservedCareerArcCausalProductionEntryDebugScenarios
                            .LastLessonActionId,
                        "observed-state",
                        ObservedCareerArcStateKeys()),
                    Edge(
                        ObservedCareerArcCausalProductionEntryDebugScenarios
                            .LastLessonActionId,
                        ObservedCareerArcCausalProductionEntryDebugScenarios
                            .QuietPromotionActionId,
                        "observed-state",
                        ObservedCareerArcStateKeys()),
                    Edge(
                        ObservedCareerArcCausalProductionEntryDebugScenarios
                            .QuietPromotionActionId,
                        ObservedCareerArcCausalProductionEntryDebugScenarios
                            .FuneralActionId,
                        "observed-state",
                        ObservedCareerArcStateKeys()),
                    Edge(
                        ObservedCareerArcCausalProductionEntryDebugScenarios
                            .FuneralActionId,
                        ObservedCareerArcCausalProductionEntryDebugScenarios
                            .LineageCompressionActionId,
                        "observed-state",
                        ObservedCareerArcStateKeys())
                }),
            Template(
                "observed-career-mentorship-award-episode",
                "single-persistent-observed-career-arc-owner",
                new[]
                {
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .MentorshipAwardActionId
                },
                Array.Empty<string>(),
                ObservedCareerArcInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "observed-career-last-lesson-episode",
                "single-persistent-observed-career-arc-owner",
                new[]
                {
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .LastLessonActionId
                },
                Array.Empty<string>(),
                ObservedCareerArcInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "observed-career-promotion-episode",
                "single-persistent-observed-career-arc-owner",
                new[]
                {
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .QuietPromotionActionId
                },
                Array.Empty<string>(),
                ObservedCareerArcInitialPredicates(stage: 2),
                Array.Empty<JObject>()),
            Template(
                "observed-career-funeral-episode",
                "single-persistent-observed-career-arc-owner",
                new[]
                {
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .FuneralActionId
                },
                Array.Empty<string>(),
                ObservedCareerArcInitialPredicates(stage: 3),
                Array.Empty<JObject>()),
            Template(
                "observed-career-lineage-compression-episode",
                "single-persistent-observed-career-arc-owner",
                new[]
                {
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .LineageCompressionActionId
                },
                Array.Empty<string>(),
                ObservedCareerArcInitialPredicates(stage: 4),
                Array.Empty<JObject>()),
            Template(
                "observed-meal-incident-resolution-sequence",
                "single-persistent-observed-meal-incident-resolution-owner",
                new[]
                {
                    ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                        .ObserveIncidentActionId,
                    ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                        .ResolveIncidentActionId
                },
                Array.Empty<string>(),
                ObservedMealIncidentResolutionInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                            .ObserveIncidentActionId,
                        ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                            .ResolveIncidentActionId,
                        "observed-state",
                        ObservedMealIncidentResolutionStateKeys())
                }),
            Template(
                "observed-meal-incident-episode",
                "single-persistent-observed-meal-incident-resolution-owner",
                new[]
                {
                    ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                        .ObserveIncidentActionId
                },
                Array.Empty<string>(),
                ObservedMealIncidentResolutionInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "observed-meal-resolution-episode",
                "single-persistent-observed-meal-incident-resolution-owner",
                new[]
                {
                    ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                        .ResolveIncidentActionId
                },
                Array.Empty<string>(),
                ObservedMealIncidentResolutionInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "dungeon-defense-social-economy",
                "multi-subject-world-events",
                new[]
                {
                    "action:trigger-defense-facility",
                    "action:execute-offense-battle-command"
                },
                new[]
                {
                    "action:adjust-faction-trust",
                    "action:purchase-retail-item",
                    "action:resolve-combat-attack"
                },
                Array.Empty<JObject>(),
                new[]
                {
                    Edge("action:trigger-defense-facility", "action:execute-offense-battle-command", "ordering-only"),
                    Edge("action:execute-offense-battle-command", "action:adjust-faction-trust", "ordering-only"),
                    Edge("action:adjust-faction-trust", "action:purchase-retail-item", "ordering-only"),
                    Edge("action:execute-offense-battle-command", "action:resolve-combat-attack", "ordering-only")
                }),
            Template(
                "detox-and-recovery-routine",
                "separate-treatment-subject-and-protagonist",
                new[]
                {
                    "action:apply-detox-treatment",
                    "action:drink-clean-water",
                    "action:rest"
                },
                new[] { "action:train-proficiency" },
                new[]
                {
                    Predicate("initial", "actor.thirst", "less-or-equal", 20),
                    Predicate("initial", "actor.sleep", "less-or-equal", 20)
                },
                new[]
                {
                    Edge("action:apply-detox-treatment", "action:drink-clean-water", "ordering-only"),
                    Edge("action:drink-clean-water", "action:rest", "committed-outcome"),
                    Edge("action:rest", "action:train-proficiency", "observed-state", "actor.sleep")
                }),
            Template(
                "crop-irrigation-growth-harvest",
                "single-persistent-crop-plot",
                new[]
                {
                    "action:environment-crop-irrigation-supplied",
                    "action:environment-crop-plan-terminal"
                },
                Array.Empty<string>(),
                Array.Empty<JObject>(),
                new[]
                {
                    Edge(
                        "action:environment-crop-irrigation-supplied",
                        "action:environment-crop-plan-terminal",
                        "observed-state",
                        "crop.phase",
                        "crop.currentWater",
                        "crop.growthProgress")
                }),
            Template(
                "crop-irrigation-episode",
                "single-persistent-crop-plot",
                new[]
                {
                    "action:environment-crop-irrigation-supplied"
                },
                Array.Empty<string>(),
                Array.Empty<JObject>(),
                Array.Empty<JObject>()),
            Template(
                "crop-harvest-episode",
                "single-persistent-crop-plot",
                new[]
                {
                    "action:environment-crop-plan-terminal"
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "crop.phase", "equal", "Growing")
                },
                Array.Empty<JObject>()),
            Template(
                "environment-fire-ignition-initial-attack",
                "single-persistent-environmental-fire",
                new[]
                {
                    "action:environment-fire-ignite",
                    "action:environment-fire-initial-attack"
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "fire.phase", "equal", "None")
                },
                new[]
                {
                    Edge(
                        "action:environment-fire-ignite",
                        "action:environment-fire-initial-attack",
                        "observed-state",
                        "fire.phase", "fire.fireId", "fire.intensity")
                }),
            Template(
                "environment-fire-fuel-damage-water",
                "single-persistent-environmental-fire-with-physical-resources",
                new[]
                {
                    "action:environment-fire-fuel-ignition",
                    "action:environment-fire-damage-tick",
                    "action:environment-fire-water-suppression"
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "fire.phase", "equal", "None"),
                    Predicate("initial", "fire.fuelStackQuantity", "equal", 2)
                },
                new[]
                {
                    Edge(
                        "action:environment-fire-fuel-ignition",
                        "action:environment-fire-damage-tick",
                        "observed-state",
                        "fire.phase", "fire.fireId", "fire.intensity",
                        "fire.fuelStackQuantity"),
                    Edge(
                        "action:environment-fire-damage-tick",
                        "action:environment-fire-water-suppression",
                        "observed-state",
                        "fire.phase", "fire.totalDamage",
                        "fire.targetHitPoints")
                }),
            Template(
                "environment-fire-ignite-episode",
                "single-persistent-environmental-fire",
                new[] { "action:environment-fire-ignite" },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "fire.phase", "equal", "None")
                },
                Array.Empty<JObject>()),
            Template(
                "environment-fire-initial-attack-episode",
                "single-persistent-environmental-fire",
                new[] { "action:environment-fire-initial-attack" },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "fire.phase", "equal", "Active"),
                    Predicate("initial", "fire.plainIgnited", "equal", true)
                },
                Array.Empty<JObject>()),
            Template(
                "environment-fire-fuel-ignition-episode",
                "single-persistent-environmental-fire-with-physical-resources",
                new[] { "action:environment-fire-fuel-ignition" },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "fire.phase", "equal", "None"),
                    Predicate("initial", "fire.fuelStackQuantity", "equal", 2)
                },
                Array.Empty<JObject>()),
            Template(
                "environment-fire-damage-episode",
                "single-persistent-environmental-fire-with-physical-resources",
                new[] { "action:environment-fire-damage-tick" },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "fire.phase", "equal", "Active"),
                    Predicate("initial", "fire.fuelIgnited", "equal", true)
                },
                Array.Empty<JObject>()),
            Template(
                "environment-fire-water-suppression-episode",
                "single-persistent-environmental-fire-with-physical-resources",
                new[] { "action:environment-fire-water-suppression" },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "fire.phase", "equal", "Active"),
                    Predicate("initial", "fire.damageApplied", "equal", true)
                },
                Array.Empty<JObject>()),
            Template(
                "room-environment-experience-condition",
                "single-persistent-room-environment",
                new[]
                {
                    RoomEnvironmentCausalProductionEntryDebugScenarios
                        .ExperienceActionId,
                    RoomEnvironmentCausalProductionEntryDebugScenarios
                        .ConditionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "roomEnvironment.stage", "equal", 0),
                    Predicate("initial", "roomEnvironment.exactOutcomeCount", "equal", 0),
                    Predicate("initial", "roomEnvironment.experienceApplied", "equal", false),
                    Predicate("initial", "roomEnvironment.conditionChanged", "equal", false)
                },
                new[]
                {
                    Edge(
                        RoomEnvironmentCausalProductionEntryDebugScenarios
                            .ExperienceActionId,
                        RoomEnvironmentCausalProductionEntryDebugScenarios
                            .ConditionActionId,
                        "observed-state",
                        "roomEnvironment.stage",
                        "roomEnvironment.observedCleanliness",
                        "roomEnvironment.nextOutcomeSequence",
                        "roomEnvironment.mood",
                        "roomEnvironment.roomMoodFactorCount",
                        "roomEnvironment.exactOutcomeCount",
                        "roomEnvironment.experienceApplied")
                }),
            Template(
                "room-environment-experience-episode",
                "single-persistent-room-environment",
                new[]
                {
                    RoomEnvironmentCausalProductionEntryDebugScenarios
                        .ExperienceActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "roomEnvironment.stage", "equal", 0),
                    Predicate("initial", "roomEnvironment.exactOutcomeCount", "equal", 0),
                    Predicate("initial", "roomEnvironment.experienceApplied", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "room-environment-condition-episode",
                "single-persistent-room-environment",
                new[]
                {
                    RoomEnvironmentCausalProductionEntryDebugScenarios
                        .ConditionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "roomEnvironment.stage", "equal", 1),
                    Predicate("initial", "roomEnvironment.nextOutcomeSequence", "equal", 2),
                    Predicate("initial", "roomEnvironment.exactOutcomeCount", "equal", 1),
                    Predicate("initial", "roomEnvironment.experienceApplied", "equal", true),
                    Predicate("initial", "roomEnvironment.conditionChanged", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "species-golem-overload-recurrence",
                "single-persistent-species-incident-owner",
                new[]
                {
                    EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                        .SpeciesFirstIncidentActionId,
                    EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                        .SpeciesRepeatIncidentActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "speciesIncident.stage", "equal", 0),
                    Predicate("initial", "speciesIncident.incidentCount", "equal", 0),
                    Predicate("initial", "speciesIncident.facilityDamaged", "equal", false),
                    Predicate("initial", "speciesIncident.exactOutcomeCount", "equal", 0)
                },
                new[]
                {
                    Edge(
                        EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                            .SpeciesFirstIncidentActionId,
                        EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                            .SpeciesRepeatIncidentActionId,
                        "observed-state",
                        "speciesIncident.stage",
                        "speciesIncident.incidentId",
                        "speciesIncident.incidentCount",
                        "speciesIncident.nextIncidentAt",
                        "speciesIncident.charge",
                        "speciesIncident.integrity",
                        "speciesIncident.actorHealth",
                        "speciesIncident.facilityDamaged",
                        "speciesIncident.exactOutcomeCount",
                        "speciesIncident.firstExecuted")
                }),
            Template(
                "species-golem-overload-first-episode",
                "single-persistent-species-incident-owner",
                new[]
                {
                    EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                        .SpeciesFirstIncidentActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "speciesIncident.stage", "equal", 0),
                    Predicate("initial", "speciesIncident.incidentCount", "equal", 0),
                    Predicate("initial", "speciesIncident.firstExecuted", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "species-golem-overload-repeat-episode",
                "single-persistent-species-incident-owner",
                new[]
                {
                    EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                        .SpeciesRepeatIncidentActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "speciesIncident.stage", "equal", 1),
                    Predicate("initial", "speciesIncident.incidentCount", "equal", 1),
                    Predicate("initial", "speciesIncident.facilityDamaged", "equal", true),
                    Predicate("initial", "speciesIncident.firstExecuted", "equal", true),
                    Predicate("initial", "speciesIncident.repeatExecuted", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "certified-seed-repeated-completion",
                "single-persistent-certified-seed-owner",
                new[]
                {
                    EnvironmentCropProductionEntryDebugScenarios
                        .CertifiedSeedFirstCompletionActionId,
                    EnvironmentCropProductionEntryDebugScenarios
                        .CertifiedSeedRepeatCompletionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "certifiedSeed.stage", "equal", 0),
                    Predicate("initial", "certifiedSeed.nextOrderSequence", "equal", 0),
                    Predicate("initial", "certifiedSeed.certifiedLotCount", "equal", 0),
                    Predicate("initial", "certifiedSeed.exactOutcomeCount", "equal", 0)
                },
                new[]
                {
                    Edge(
                        EnvironmentCropProductionEntryDebugScenarios
                            .CertifiedSeedFirstCompletionActionId,
                        EnvironmentCropProductionEntryDebugScenarios
                            .CertifiedSeedRepeatCompletionActionId,
                        "observed-state",
                        "certifiedSeed.stage",
                        "certifiedSeed.nextOrderSequence",
                        "certifiedSeed.lastProcessedOperatingDay",
                        "certifiedSeed.operatingDay",
                        "certifiedSeed.certifiedLotCount",
                        "certifiedSeed.exactOutcomeCount",
                        "certifiedSeed.firstExecuted")
                }),
            Template(
                "certified-seed-first-completion-episode",
                "single-persistent-certified-seed-owner",
                new[]
                {
                    EnvironmentCropProductionEntryDebugScenarios
                        .CertifiedSeedFirstCompletionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "certifiedSeed.stage", "equal", 0),
                    Predicate("initial", "certifiedSeed.nextOrderSequence", "equal", 0),
                    Predicate("initial", "certifiedSeed.firstExecuted", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "certified-seed-repeat-completion-episode",
                "single-persistent-certified-seed-owner",
                new[]
                {
                    EnvironmentCropProductionEntryDebugScenarios
                        .CertifiedSeedRepeatCompletionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "certifiedSeed.stage", "equal", 1),
                    Predicate("initial", "certifiedSeed.nextOrderSequence", "equal", 1),
                    Predicate("initial", "certifiedSeed.lastProcessedOperatingDay", "equal", 23),
                    Predicate("initial", "certifiedSeed.certifiedLotCount", "equal", 1),
                    Predicate("initial", "certifiedSeed.firstExecuted", "equal", true),
                    Predicate("initial", "certifiedSeed.repeatExecuted", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "social-assault-conflict-apology",
                "single-persistent-social-relationship",
                new[]
                {
                    SocialLifeCausalProductionEntryDebugScenarios
                        .AssaultConflictActionId,
                    SocialLifeCausalProductionEntryDebugScenarios
                        .ApologyActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "social.canForgive", "equal", false),
                    Predicate("initial", "social.assaultExecuted", "equal", false)
                },
                new[]
                {
                    Edge(
                        SocialLifeCausalProductionEntryDebugScenarios
                            .AssaultConflictActionId,
                        SocialLifeCausalProductionEntryDebugScenarios
                            .ApologyActionId,
                        "observed-state",
                        "social.canForgive",
                        "social.identityRuleStateCount",
                        "social.relationshipSentiment",
                        "social.recipientHealth")
                }),
            Template(
                "social-assault-conflict-episode",
                "single-persistent-social-relationship",
                new[]
                {
                    SocialLifeCausalProductionEntryDebugScenarios
                        .AssaultConflictActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "social.canForgive", "equal", false),
                    Predicate("initial", "social.assaultExecuted", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "social-apology-episode",
                "single-persistent-social-relationship",
                new[]
                {
                    SocialLifeCausalProductionEntryDebugScenarios.ApologyActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "social.canForgive", "equal", true),
                    Predicate("initial", "social.assaultExecuted", "equal", true),
                    Predicate("initial", "social.apologyExecuted", "equal", false)
                },
                Array.Empty<JObject>())
            ,
            Template(
                "staff-rebellion-isolation-calming",
                "single-persistent-staff-discontent-owner",
                new[]
                {
                    StaffRebellionResponseDebugScenarios.CausalIsolateActionId,
                    StaffRebellionResponseDebugScenarios.CausalCalmActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "staffResponse.stage", "equal", 0),
                    Predicate("initial", "staffResponse.outcomeRevision", "equal", 0L),
                    Predicate("initial", "staffResponse.rebelStage", "equal", "LocalRebellion"),
                    Predicate("initial", "staffResponse.rebelIsolated", "equal", false),
                    Predicate("initial", "staffResponse.workerStage", "equal", "WorkDisruption")
                },
                new[]
                {
                    Edge(
                        StaffRebellionResponseDebugScenarios.CausalIsolateActionId,
                        StaffRebellionResponseDebugScenarios.CausalCalmActionId,
                        "observed-state",
                        "staffResponse.stage",
                        "staffResponse.outcomeRevision",
                        "staffResponse.rebelIsolated",
                        "staffResponse.workerStage",
                        "staffResponse.workerMood",
                        "staffResponse.exactOutcomeCount",
                        "staffResponse.isolationExecuted")
                }),
            Template(
                "staff-rebellion-isolation-episode",
                "single-persistent-staff-discontent-owner",
                new[]
                {
                    StaffRebellionResponseDebugScenarios.CausalIsolateActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "staffResponse.stage", "equal", 0),
                    Predicate("initial", "staffResponse.outcomeRevision", "equal", 0L),
                    Predicate("initial", "staffResponse.rebelIsolated", "equal", false),
                    Predicate("initial", "staffResponse.isolationExecuted", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "staff-discontent-calming-episode",
                "single-persistent-staff-discontent-owner",
                new[]
                {
                    StaffRebellionResponseDebugScenarios.CausalCalmActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "staffResponse.stage", "equal", 1),
                    Predicate("initial", "staffResponse.outcomeRevision", "equal", 1L),
                    Predicate("initial", "staffResponse.rebelIsolated", "equal", true),
                    Predicate("initial", "staffResponse.workerStage", "equal", "WorkDisruption"),
                    Predicate("initial", "staffResponse.isolationExecuted", "equal", true),
                    Predicate("initial", "staffResponse.calmExecuted", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "emergency-work-suspension-sequence",
                "single-persistent-emergency-work-suspension-owner",
                new[]
                {
                    EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                        .FirstSuspensionActionId,
                    EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                        .SecondSuspensionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "emergencySuspension.stage", "equal", 0),
                    Predicate("initial", "emergencySuspension.suspendedWorkCount", "equal", 0),
                    Predicate("initial", "emergencySuspension.firstRecorded", "equal", false),
                    Predicate("initial", "emergencySuspension.secondRecorded", "equal", false),
                    Predicate("initial", "emergencySuspension.exactOutcomeCount", "equal", 0)
                },
                new[]
                {
                    Edge(
                        EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                            .FirstSuspensionActionId,
                        EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                            .SecondSuspensionActionId,
                        "observed-state",
                        "emergencySuspension.stage",
                        "emergencySuspension.alertEpoch",
                        "emergencySuspension.calendarAbsoluteHour",
                        "emergencySuspension.suspendedWorkCount",
                        "emergencySuspension.firstRecorded",
                        "emergencySuspension.firstCompletedWork",
                        "emergencySuspension.firstRequiredWork",
                        "emergencySuspension.exactOutcomeCount")
                }),
            Template(
                "emergency-work-suspension-first-episode",
                "single-persistent-emergency-work-suspension-owner",
                new[]
                {
                    EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                        .FirstSuspensionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "emergencySuspension.stage", "equal", 0),
                    Predicate("initial", "emergencySuspension.suspendedWorkCount", "equal", 0),
                    Predicate("initial", "emergencySuspension.firstRecorded", "equal", false),
                    Predicate("initial", "emergencySuspension.secondRecorded", "equal", false),
                    Predicate("initial", "emergencySuspension.exactOutcomeCount", "equal", 0)
                },
                Array.Empty<JObject>()),
            Template(
                "emergency-work-suspension-second-episode",
                "single-persistent-emergency-work-suspension-owner",
                new[]
                {
                    EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                        .SecondSuspensionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "emergencySuspension.stage", "equal", 1),
                    Predicate("initial", "emergencySuspension.suspendedWorkCount", "equal", 1),
                    Predicate("initial", "emergencySuspension.firstRecorded", "equal", true),
                    Predicate("initial", "emergencySuspension.secondRecorded", "equal", false),
                    Predicate("initial", "emergencySuspension.firstCompletedWork", "equal", 2f),
                    Predicate("initial", "emergencySuspension.firstRequiredWork", "equal", 10f),
                    Predicate("initial", "emergencySuspension.exactOutcomeCount", "equal", 1)
                },
                Array.Empty<JObject>()),
            Template(
                "memory-erasure-boss-award-sequence",
                "single-persistent-memory-erasure-boss-award-owner",
                new[]
                {
                    MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                        .FirstAwardActionId,
                    MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                        .SecondAwardActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "bossAward.stage", "equal", 0),
                    Predicate("initial", "bossAward.awardedRegionCount", "equal", 0),
                    Predicate("initial", "bossAward.physicalPublicationCount", "equal", 0),
                    Predicate("initial", "bossAward.firstAwarded", "equal", false),
                    Predicate("initial", "bossAward.secondAwarded", "equal", false),
                    Predicate("initial", "bossAward.totalSealQuantity", "equal", 0),
                    Predicate("initial", "bossAward.exactOutcomeCount", "equal", 0)
                },
                new[]
                {
                    Edge(
                        MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                            .FirstAwardActionId,
                        MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                            .SecondAwardActionId,
                        "observed-state",
                        "bossAward.stage",
                        "bossAward.awardedRegionCount",
                        "bossAward.physicalPublicationCount",
                        "bossAward.firstAwarded",
                        "bossAward.firstSealQuantity",
                        "bossAward.totalSealQuantity",
                        "bossAward.exactOutcomeCount")
                }),
            Template(
                "memory-erasure-first-boss-award-episode",
                "single-persistent-memory-erasure-boss-award-owner",
                new[]
                {
                    MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                        .FirstAwardActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "bossAward.stage", "equal", 0),
                    Predicate("initial", "bossAward.awardedRegionCount", "equal", 0),
                    Predicate("initial", "bossAward.firstAwarded", "equal", false),
                    Predicate("initial", "bossAward.secondAwarded", "equal", false),
                    Predicate("initial", "bossAward.totalSealQuantity", "equal", 0),
                    Predicate("initial", "bossAward.exactOutcomeCount", "equal", 0)
                },
                Array.Empty<JObject>()),
            Template(
                "memory-erasure-second-boss-award-episode",
                "single-persistent-memory-erasure-boss-award-owner",
                new[]
                {
                    MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                        .SecondAwardActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "bossAward.stage", "equal", 1),
                    Predicate("initial", "bossAward.awardedRegionCount", "equal", 1),
                    Predicate("initial", "bossAward.physicalPublicationCount", "equal", 1),
                    Predicate("initial", "bossAward.firstAwarded", "equal", true),
                    Predicate("initial", "bossAward.secondAwarded", "equal", false),
                    Predicate("initial", "bossAward.firstSealQuantity", "equal", 1),
                    Predicate("initial", "bossAward.totalSealQuantity", "equal", 1),
                    Predicate("initial", "bossAward.exactOutcomeCount", "equal", 1)
                },
                Array.Empty<JObject>()),
            Template(
                "trade-inventory-three-producer-sequence",
                "single-persistent-trade-inventory-owner",
                new[]
                {
                    TradeInventoryCausalProductionEntryDebugScenarios
                        .RelocateActionId,
                    TradeInventoryCausalProductionEntryDebugScenarios
                        .DisposeActionId,
                    TradeInventoryCausalProductionEntryDebugScenarios
                        .WastePolicyActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "tradeInventory.stage", "equal", 0),
                    Predicate("initial", "tradeInventory.sourceQuantity", "equal", 3),
                    Predicate("initial", "tradeInventory.relocatedQuantity", "equal", 0),
                    Predicate("initial", "tradeInventory.stackCount", "equal", 1),
                    Predicate("initial", "tradeInventory.wasteDisposition", "equal", "Store"),
                    Predicate("initial", "tradeInventory.wasteEnabled", "equal", true),
                    Predicate("initial", "tradeInventory.wasteMaximumPercent", "equal", 79f),
                    Predicate("initial", "tradeInventory.exactOutcomeCount", "equal", 0)
                },
                new[]
                {
                    Edge(
                        TradeInventoryCausalProductionEntryDebugScenarios
                            .RelocateActionId,
                        TradeInventoryCausalProductionEntryDebugScenarios
                            .DisposeActionId,
                        "observed-state",
                        "tradeInventory.stage",
                        "tradeInventory.sourceQuantity",
                        "tradeInventory.relocatedQuantity",
                        "tradeInventory.stackCount",
                        "tradeInventory.relocatedPresent",
                        "tradeInventory.relocationOutcomeCount",
                        "tradeInventory.exactOutcomeCount"),
                    Edge(
                        TradeInventoryCausalProductionEntryDebugScenarios
                            .DisposeActionId,
                        TradeInventoryCausalProductionEntryDebugScenarios
                            .WastePolicyActionId,
                        "observed-state",
                        "tradeInventory.stage",
                        "tradeInventory.sourceQuantity",
                        "tradeInventory.relocatedQuantity",
                        "tradeInventory.stackCount",
                        "tradeInventory.relocatedPresent",
                        "tradeInventory.relocationOutcomeCount",
                        "tradeInventory.dispositionOutcomeCount",
                        "tradeInventory.exactOutcomeCount")
                }),
            Template(
                "trade-inventory-relocation-episode",
                "single-persistent-trade-inventory-owner",
                new[]
                {
                    TradeInventoryCausalProductionEntryDebugScenarios
                        .RelocateActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "tradeInventory.stage", "equal", 0),
                    Predicate("initial", "tradeInventory.sourceQuantity", "equal", 3),
                    Predicate("initial", "tradeInventory.relocatedQuantity", "equal", 0),
                    Predicate("initial", "tradeInventory.stackCount", "equal", 1),
                    Predicate("initial", "tradeInventory.exactOutcomeCount", "equal", 0)
                },
                Array.Empty<JObject>()),
            Template(
                "trade-inventory-disposition-episode",
                "single-persistent-trade-inventory-owner",
                new[]
                {
                    TradeInventoryCausalProductionEntryDebugScenarios
                        .DisposeActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "tradeInventory.stage", "equal", 1),
                    Predicate("initial", "tradeInventory.sourceQuantity", "equal", 2),
                    Predicate("initial", "tradeInventory.relocatedQuantity", "equal", 1),
                    Predicate("initial", "tradeInventory.stackCount", "equal", 2),
                    Predicate("initial", "tradeInventory.relocatedPresent", "equal", true),
                    Predicate("initial", "tradeInventory.relocationOutcomeCount", "equal", 1),
                    Predicate("initial", "tradeInventory.exactOutcomeCount", "equal", 1)
                },
                Array.Empty<JObject>()),
            Template(
                "trade-inventory-waste-policy-episode",
                "single-persistent-trade-inventory-owner",
                new[]
                {
                    TradeInventoryCausalProductionEntryDebugScenarios
                        .WastePolicyActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "tradeInventory.stage", "equal", 2),
                    Predicate("initial", "tradeInventory.sourceQuantity", "equal", 2),
                    Predicate("initial", "tradeInventory.relocatedQuantity", "equal", 0),
                    Predicate("initial", "tradeInventory.stackCount", "equal", 1),
                    Predicate("initial", "tradeInventory.relocatedPresent", "equal", false),
                    Predicate("initial", "tradeInventory.wasteDisposition", "equal", "Store"),
                    Predicate("initial", "tradeInventory.wasteEnabled", "equal", true),
                    Predicate("initial", "tradeInventory.wasteMaximumPercent", "equal", 79f),
                    Predicate("initial", "tradeInventory.relocationOutcomeCount", "equal", 1),
                    Predicate("initial", "tradeInventory.dispositionOutcomeCount", "equal", 1),
                    Predicate("initial", "tradeInventory.exactOutcomeCount", "equal", 2)
                },
                Array.Empty<JObject>()),
            Template(
                "disease-route-exposure-sequence",
                "single-persistent-disease-route-owner",
                new[]
                {
                    DiseaseRouteExposureCausalProductionEntryDebugScenarios
                        .FirstExposureActionId,
                    DiseaseRouteExposureCausalProductionEntryDebugScenarios
                        .RepeatExposureActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "diseaseRoute.stage", "equal", 0),
                    Predicate("initial", "diseaseRoute.calendarDay", "equal", 17),
                    Predicate("initial", "diseaseRoute.lastPublishedAbsoluteDay", "equal", 16),
                    Predicate("initial", "diseaseRoute.healthVersion", "equal", 1),
                    Predicate("initial", "diseaseRoute.pendingExposureCount", "equal", 0),
                    Predicate("initial", "diseaseRoute.weightedExposureHours", "equal", 0f),
                    Predicate("initial", "diseaseRoute.exactOutcomeCount", "equal", 0)
                },
                new[]
                {
                    Edge(
                        DiseaseRouteExposureCausalProductionEntryDebugScenarios
                            .FirstExposureActionId,
                        DiseaseRouteExposureCausalProductionEntryDebugScenarios
                            .RepeatExposureActionId,
                        "observed-state",
                        "diseaseRoute.stage",
                        "diseaseRoute.lastPublishedAbsoluteDay",
                        "diseaseRoute.healthVersion",
                        "diseaseRoute.pendingExposureCount",
                        "diseaseRoute.weightedExposureHours",
                        "diseaseRoute.exactOutcomeCount")
                }),
            Template(
                "disease-route-first-exposure-episode",
                "single-persistent-disease-route-owner",
                new[]
                {
                    DiseaseRouteExposureCausalProductionEntryDebugScenarios
                        .FirstExposureActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "diseaseRoute.stage", "equal", 0),
                    Predicate("initial", "diseaseRoute.calendarDay", "equal", 17),
                    Predicate("initial", "diseaseRoute.lastPublishedAbsoluteDay", "equal", 16),
                    Predicate("initial", "diseaseRoute.healthVersion", "equal", 1),
                    Predicate("initial", "diseaseRoute.pendingExposureCount", "equal", 0),
                    Predicate("initial", "diseaseRoute.weightedExposureHours", "equal", 0f),
                    Predicate("initial", "diseaseRoute.exactOutcomeCount", "equal", 0)
                },
                Array.Empty<JObject>()),
            Template(
                "disease-route-repeat-exposure-episode",
                "single-persistent-disease-route-owner",
                new[]
                {
                    DiseaseRouteExposureCausalProductionEntryDebugScenarios
                        .RepeatExposureActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "diseaseRoute.stage", "equal", 1),
                    Predicate("initial", "diseaseRoute.calendarDay", "equal", 17),
                    Predicate("initial", "diseaseRoute.lastPublishedAbsoluteDay", "equal", 17),
                    Predicate("initial", "diseaseRoute.healthVersion", "equal", 2),
                    Predicate("initial", "diseaseRoute.pendingExposureCount", "equal", 1),
                    Predicate("initial", "diseaseRoute.weightedExposureHours", "equal", 1f),
                    Predicate("initial", "diseaseRoute.exactOutcomeCount", "equal", 1)
                },
                Array.Empty<JObject>()),
            Template(
                "offense-node-resolution-sequence",
                "single-persistent-offense-node-resolution-owner",
                new[]
                {
                    OffenseNodeResolutionCausalProductionEntryDebugScenarios
                        .ClearApproachActionId,
                    OffenseNodeResolutionCausalProductionEntryDebugScenarios
                        .SecureCacheActionId
                },
                Array.Empty<string>(),
                OffenseNodeResolutionInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        OffenseNodeResolutionCausalProductionEntryDebugScenarios
                            .ClearApproachActionId,
                        OffenseNodeResolutionCausalProductionEntryDebugScenarios
                            .SecureCacheActionId,
                        "observed-state",
                        OffenseNodeResolutionStateKeys())
                }),
            Template(
                "offense-node-clear-approach-episode",
                "single-persistent-offense-node-resolution-owner",
                new[]
                {
                    OffenseNodeResolutionCausalProductionEntryDebugScenarios
                        .ClearApproachActionId
                },
                Array.Empty<string>(),
                OffenseNodeResolutionInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "offense-node-secure-cache-episode",
                "single-persistent-offense-node-resolution-owner",
                new[]
                {
                    OffenseNodeResolutionCausalProductionEntryDebugScenarios
                        .SecureCacheActionId
                },
                Array.Empty<string>(),
                OffenseNodeResolutionInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "offense-truth-reveal-sequence",
                "single-persistent-offense-truth-reveal-owner",
                new[]
                {
                    OffenseTruthRevealCausalProductionEntryDebugScenarios
                        .PreliminaryVictoryActionId,
                    OffenseTruthRevealCausalProductionEntryDebugScenarios
                        .RevealTruthActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "offenseTruth.stage", "equal", 0),
                    Predicate("initial", "offenseTruth.absoluteDay", "equal", 31),
                    Predicate("initial", "offenseTruth.knownTargetCount", "equal", 2),
                    Predicate("initial", "offenseTruth.completedTargetCount", "equal", 0),
                    Predicate("initial", "offenseTruth.preliminaryKnown", "equal", true),
                    Predicate("initial", "offenseTruth.preliminaryCompleted", "equal", false),
                    Predicate("initial", "offenseTruth.truthKnown", "equal", true),
                    Predicate("initial", "offenseTruth.truthCompleted", "equal", false),
                    Predicate("initial", "offenseTruth.truthRevealed", "equal", false),
                    Predicate("initial", "offenseTruth.revealedTargetId", "equal", string.Empty),
                    Predicate("initial", "offenseTruth.migratedOwnerRevisionCount", "equal", 0),
                    Predicate("initial", "offenseTruth.preliminaryOutcomeCount", "equal", 0),
                    Predicate("initial", "offenseTruth.truthOutcomeCount", "equal", 0),
                    Predicate("initial", "offenseTruth.exactOutcomeCount", "equal", 0)
                },
                new[]
                {
                    Edge(
                        OffenseTruthRevealCausalProductionEntryDebugScenarios
                            .PreliminaryVictoryActionId,
                        OffenseTruthRevealCausalProductionEntryDebugScenarios
                            .RevealTruthActionId,
                        "observed-state",
                        "offenseTruth.stage",
                        "offenseTruth.completedTargetCount",
                        "offenseTruth.preliminaryCompleted",
                        "offenseTruth.migratedOwnerRevisionCount",
                        "offenseTruth.preliminaryOutcomeCount",
                        "offenseTruth.exactOutcomeCount")
                }),
            Template(
                "offense-truth-preliminary-victory-episode",
                "single-persistent-offense-truth-reveal-owner",
                new[]
                {
                    OffenseTruthRevealCausalProductionEntryDebugScenarios
                        .PreliminaryVictoryActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "offenseTruth.stage", "equal", 0),
                    Predicate("initial", "offenseTruth.absoluteDay", "equal", 31),
                    Predicate("initial", "offenseTruth.knownTargetCount", "equal", 2),
                    Predicate("initial", "offenseTruth.completedTargetCount", "equal", 0),
                    Predicate("initial", "offenseTruth.preliminaryKnown", "equal", true),
                    Predicate("initial", "offenseTruth.preliminaryCompleted", "equal", false),
                    Predicate("initial", "offenseTruth.truthKnown", "equal", true),
                    Predicate("initial", "offenseTruth.truthCompleted", "equal", false),
                    Predicate("initial", "offenseTruth.truthRevealed", "equal", false),
                    Predicate("initial", "offenseTruth.revealedTargetId", "equal", string.Empty),
                    Predicate("initial", "offenseTruth.migratedOwnerRevisionCount", "equal", 0),
                    Predicate("initial", "offenseTruth.preliminaryOutcomeCount", "equal", 0),
                    Predicate("initial", "offenseTruth.truthOutcomeCount", "equal", 0),
                    Predicate("initial", "offenseTruth.exactOutcomeCount", "equal", 0)
                },
                Array.Empty<JObject>()),
            Template(
                "offense-truth-final-reveal-episode",
                "single-persistent-offense-truth-reveal-owner",
                new[]
                {
                    OffenseTruthRevealCausalProductionEntryDebugScenarios
                        .RevealTruthActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "offenseTruth.stage", "equal", 1),
                    Predicate("initial", "offenseTruth.absoluteDay", "equal", 31),
                    Predicate("initial", "offenseTruth.knownTargetCount", "equal", 2),
                    Predicate("initial", "offenseTruth.completedTargetCount", "equal", 1),
                    Predicate("initial", "offenseTruth.preliminaryKnown", "equal", true),
                    Predicate("initial", "offenseTruth.preliminaryCompleted", "equal", true),
                    Predicate("initial", "offenseTruth.truthKnown", "equal", true),
                    Predicate("initial", "offenseTruth.truthCompleted", "equal", false),
                    Predicate("initial", "offenseTruth.truthRevealed", "equal", false),
                    Predicate("initial", "offenseTruth.revealedTargetId", "equal", string.Empty),
                    Predicate("initial", "offenseTruth.migratedOwnerRevisionCount", "equal", 1),
                    Predicate("initial", "offenseTruth.preliminaryOutcomeCount", "equal", 1),
                    Predicate("initial", "offenseTruth.truthOutcomeCount", "equal", 0),
                    Predicate("initial", "offenseTruth.exactOutcomeCount", "equal", 1)
                },
                Array.Empty<JObject>()),
            Template(
                "work-identity-harvest-sequence",
                "single-persistent-work-completion-identity-owner",
                new[]
                {
                    WorkCompletionIdentityCausalProductionEntryDebugScenarios
                        .FirstHarvestActionId,
                    WorkCompletionIdentityCausalProductionEntryDebugScenarios
                        .NextDayHarvestActionId
                },
                Array.Empty<string>(),
                WorkIdentityInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        WorkCompletionIdentityCausalProductionEntryDebugScenarios
                            .FirstHarvestActionId,
                        WorkCompletionIdentityCausalProductionEntryDebugScenarios
                            .NextDayHarvestActionId,
                        "observed-state",
                        "workIdentity.stage",
                        "workIdentity.lastAppliedDay",
                        "workIdentity.deliveryOperationSequence",
                        "workIdentity.identityRuleStateCount",
                        "workIdentity.firstProcessRecorded",
                        "workIdentity.firstApplied",
                        "workIdentity.exactOutcomeCount")
                }),
            Template(
                "work-identity-first-harvest-episode",
                "single-persistent-work-completion-identity-owner",
                new[]
                {
                    WorkCompletionIdentityCausalProductionEntryDebugScenarios
                        .FirstHarvestActionId
                },
                Array.Empty<string>(),
                WorkIdentityInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "work-identity-next-day-harvest-episode",
                "single-persistent-work-completion-identity-owner",
                new[]
                {
                    WorkCompletionIdentityCausalProductionEntryDebugScenarios
                        .NextDayHarvestActionId
                },
                Array.Empty<string>(),
                WorkIdentityInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "run-operation-variable-sequence",
                "single-persistent-run-operation-variable-owner",
                new[]
                {
                    RunVariableCausalProductionEntryDebugScenarios
                        .ActivateActionId,
                    RunVariableCausalProductionEntryDebugScenarios
                        .ExpireActionId
                },
                Array.Empty<string>(),
                RunVariableInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        RunVariableCausalProductionEntryDebugScenarios
                            .ActivateActionId,
                        RunVariableCausalProductionEntryDebugScenarios
                            .ExpireActionId,
                        "observed-state",
                        "runVariable.stage",
                        "runVariable.activeCount",
                        "runVariable.activeVariableId",
                        "runVariable.remainingDays",
                        "runVariable.instanceSequence",
                        "runVariable.nextOperationSequence",
                        "runVariable.activationOutcomeCount",
                        "runVariable.exactOutcomeCount")
                }),
            Template(
                "run-operation-variable-activate-episode",
                "single-persistent-run-operation-variable-owner",
                new[]
                {
                    RunVariableCausalProductionEntryDebugScenarios
                        .ActivateActionId
                },
                Array.Empty<string>(),
                RunVariableInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "run-operation-variable-expire-episode",
                "single-persistent-run-operation-variable-owner",
                new[]
                {
                    RunVariableCausalProductionEntryDebugScenarios
                        .ExpireActionId
                },
                Array.Empty<string>(),
                RunVariableInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "visitor-reception-checkout-sequence", "single-persistent-visitor-reception-checkout-owner",
                new[] { VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.ReceptionActionId,
                    VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.CheckoutActionId },
                Array.Empty<string>(), VisitorCheckoutInitialPredicates(0),
                new[] { Edge(VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.ReceptionActionId,
                    VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.CheckoutActionId,
                    "observed-state", VisitorCheckoutStateKeys()) }),
            Template(
                "visitor-reception-checkout-episode-0", "single-persistent-visitor-reception-checkout-owner",
                new[] { VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.ReceptionActionId },
                Array.Empty<string>(), VisitorCheckoutInitialPredicates(0), Array.Empty<JObject>()),
            Template(
                "visitor-reception-checkout-episode-1", "single-persistent-visitor-reception-checkout-owner",
                new[] { VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.CheckoutActionId },
                Array.Empty<string>(), VisitorCheckoutInitialPredicates(1), Array.Empty<JObject>()),
            Template(
                "taboo-butchery-sequence", "single-persistent-taboo-butchery-owner",
                new[] { TabooButcheryCausalProductionEntryDebugScenarios.ControlledDeathActionId,
                    TabooButcheryCausalProductionEntryDebugScenarios.ButcherCorpseActionId },
                Array.Empty<string>(), TabooButcheryInitialPredicates(0),
                new[] { Edge(TabooButcheryCausalProductionEntryDebugScenarios.ControlledDeathActionId,
                    TabooButcheryCausalProductionEntryDebugScenarios.ButcherCorpseActionId,
                    "observed-state", TabooButcheryStateKeys()) }),
            Template(
                "taboo-butchery-episode-0", "single-persistent-taboo-butchery-owner",
                new[] { TabooButcheryCausalProductionEntryDebugScenarios.ControlledDeathActionId },
                Array.Empty<string>(), TabooButcheryInitialPredicates(0), Array.Empty<JObject>()),
            Template(
                "taboo-butchery-episode-1", "single-persistent-taboo-butchery-owner",
                new[] { TabooButcheryCausalProductionEntryDebugScenarios.ButcherCorpseActionId },
                Array.Empty<string>(), TabooButcheryInitialPredicates(1), Array.Empty<JObject>()),
            Template(
                "offense-special-wildlife-arrival-sequence",
                "single-persistent-offense-special-wildlife-arrival-owner",
                new[]
                {
                    OffenseArrivalCausalProductionEntryDebugScenarios.FinalizeRewardActionId,
                    OffenseArrivalCausalProductionEntryDebugScenarios.SettleArrivalActionId
                },
                Array.Empty<string>(),
                OffenseArrivalInitialPredicates(0),
                new[]
                {
                    Edge(
                        OffenseArrivalCausalProductionEntryDebugScenarios.FinalizeRewardActionId,
                        OffenseArrivalCausalProductionEntryDebugScenarios.SettleArrivalActionId,
                        "observed-state",
                        OffenseArrivalStateKeys())
                }),
            Template(
                "offense-special-wildlife-arrival-episode-0",
                "single-persistent-offense-special-wildlife-arrival-owner",
                new[]
                {
                    OffenseArrivalCausalProductionEntryDebugScenarios.FinalizeRewardActionId
                },
                Array.Empty<string>(), OffenseArrivalInitialPredicates(0),
                Array.Empty<JObject>()),
            Template(
                "offense-special-wildlife-arrival-episode-1",
                "single-persistent-offense-special-wildlife-arrival-owner",
                new[]
                {
                    OffenseArrivalCausalProductionEntryDebugScenarios.SettleArrivalActionId
                },
                Array.Empty<string>(), OffenseArrivalInitialPredicates(1),
                Array.Empty<JObject>()),
            Template(
                "festival-long-night-memorial-sequence",
                "single-persistent-festival-execution-owner",
                new[]
                {
                    FestivalCelebrationCausalProductionEntryDebugScenarios.FirstCelebrationActionId,
                    FestivalCelebrationCausalProductionEntryDebugScenarios.SecondCelebrationActionId
                },
                Array.Empty<string>(),
                FestivalCelebrationInitialPredicates(0),
                new[]
                {
                    Edge(
                        FestivalCelebrationCausalProductionEntryDebugScenarios.FirstCelebrationActionId,
                        FestivalCelebrationCausalProductionEntryDebugScenarios.SecondCelebrationActionId,
                        "observed-state",
                        FestivalCelebrationStateKeys())
                }),
            Template(
                "festival-long-night-memorial-episode-0",
                "single-persistent-festival-execution-owner",
                new[]
                {
                    FestivalCelebrationCausalProductionEntryDebugScenarios.FirstCelebrationActionId
                },
                Array.Empty<string>(), FestivalCelebrationInitialPredicates(0),
                Array.Empty<JObject>()),
            Template(
                "festival-long-night-memorial-episode-1",
                "single-persistent-festival-execution-owner",
                new[]
                {
                    FestivalCelebrationCausalProductionEntryDebugScenarios.SecondCelebrationActionId
                },
                Array.Empty<string>(), FestivalCelebrationInitialPredicates(1),
                Array.Empty<JObject>()),
            Template(
                "owner-run-completion-sequence", "single-persistent-owner-run-completion-owner",
                new[] { OwnerRunCompletionCausalProductionEntryDebugScenarios.CompleteActionId,
                    OwnerRunCompletionCausalProductionEntryDebugScenarios.UpgradeActionId },
                Array.Empty<string>(), OwnerRunCompletionInitialPredicates(0),
                new[] { Edge(OwnerRunCompletionCausalProductionEntryDebugScenarios.CompleteActionId,
                    OwnerRunCompletionCausalProductionEntryDebugScenarios.UpgradeActionId,
                    "observed-state", OwnerRunCompletionStateKeys()) }),
            Template(
                "owner-run-completion-episode-0", "single-persistent-owner-run-completion-owner",
                new[] { OwnerRunCompletionCausalProductionEntryDebugScenarios.CompleteActionId },
                Array.Empty<string>(), OwnerRunCompletionInitialPredicates(0), Array.Empty<JObject>()),
            Template(
                "owner-run-completion-episode-1", "single-persistent-owner-run-completion-owner",
                new[] { OwnerRunCompletionCausalProductionEntryDebugScenarios.UpgradeActionId },
                Array.Empty<string>(), OwnerRunCompletionInitialPredicates(1), Array.Empty<JObject>()),
            Template(
                "meta-run-progression-sequence",
                "single-persistent-meta-run-progression-owner",
                new[]
                {
                    MetaProgressionCausalProductionEntryDebugScenarios
                        .RunResultActionId,
                    MetaProgressionCausalProductionEntryDebugScenarios
                        .UpgradeActionId
                },
                Array.Empty<string>(),
                MetaProgressionInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        MetaProgressionCausalProductionEntryDebugScenarios
                            .RunResultActionId,
                        MetaProgressionCausalProductionEntryDebugScenarios
                            .UpgradeActionId,
                        "observed-state",
                        "metaProgression.stage",
                        "metaProgression.lifetimeEarnedCurrency",
                        "metaProgression.availableCurrency",
                        "metaProgression.completedRunCount",
                        "metaProgression.ended",
                        "metaProgression.latestLegacyCurrency",
                        "metaProgression.runResultOutcomeCount",
                        "metaProgression.exactOutcomeCount")
                }),
            Template(
                "meta-run-result-ready-episode",
                "single-persistent-meta-run-progression-owner",
                new[]
                {
                    MetaProgressionCausalProductionEntryDebugScenarios
                        .RunResultActionId
                },
                Array.Empty<string>(),
                MetaProgressionInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "meta-upgrade-purchased-episode",
                "single-persistent-meta-run-progression-owner",
                new[]
                {
                    MetaProgressionCausalProductionEntryDebugScenarios
                        .UpgradeActionId
                },
                Array.Empty<string>(),
                MetaProgressionInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "regular-customer-progression-sequence",
                "single-persistent-regular-customer-owner",
                new[]
                {
                    RegularCustomerCausalProductionEntryDebugScenarios
                        .FirstVisitActionId,
                    RegularCustomerCausalProductionEntryDebugScenarios
                        .CandidateVisitActionId,
                    RegularCustomerCausalProductionEntryDebugScenarios
                        .RecruitActionId
                },
                Array.Empty<string>(),
                RegularCustomerInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        RegularCustomerCausalProductionEntryDebugScenarios
                            .FirstVisitActionId,
                        RegularCustomerCausalProductionEntryDebugScenarios
                            .CandidateVisitActionId,
                        "observed-state",
                        "regularCustomer.stage",
                        "regularCustomer.visitCount",
                        "regularCustomer.averageSatisfaction",
                        "regularCustomer.visitOutcomeCount",
                        "regularCustomer.exactOutcomeCount"),
                    Edge(
                        RegularCustomerCausalProductionEntryDebugScenarios
                            .CandidateVisitActionId,
                        RegularCustomerCausalProductionEntryDebugScenarios
                            .RecruitActionId,
                        "observed-state",
                        "regularCustomer.stage",
                        "regularCustomer.visitCount",
                        "regularCustomer.isRegular",
                        "regularCustomer.isRecruitCandidate",
                        "regularCustomer.status",
                        "regularCustomer.visitOutcomeCount",
                        "regularCustomer.exactOutcomeCount")
                }),
            Template(
                "regular-customer-first-visit-episode",
                "single-persistent-regular-customer-owner",
                new[]
                {
                    RegularCustomerCausalProductionEntryDebugScenarios
                        .FirstVisitActionId
                },
                Array.Empty<string>(),
                RegularCustomerInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "regular-customer-candidate-recruit-episode",
                "single-persistent-regular-customer-owner",
                new[]
                {
                    RegularCustomerCausalProductionEntryDebugScenarios
                        .CandidateVisitActionId,
                    RegularCustomerCausalProductionEntryDebugScenarios
                        .RecruitActionId
                },
                Array.Empty<string>(),
                RegularCustomerInitialPredicates(stage: 1),
                new[]
                {
                    Edge(
                        RegularCustomerCausalProductionEntryDebugScenarios
                            .CandidateVisitActionId,
                        RegularCustomerCausalProductionEntryDebugScenarios
                            .RecruitActionId,
                        "observed-state",
                        "regularCustomer.stage",
                        "regularCustomer.visitCount",
                        "regularCustomer.isRegular",
                        "regularCustomer.isRecruitCandidate",
                        "regularCustomer.status",
                        "regularCustomer.visitOutcomeCount",
                        "regularCustomer.exactOutcomeCount")
                }),
            Template(
                "character-life-progression-sequence",
                "single-persistent-character-life-owner",
                new[]
                {
                    CharacterLifeCausalProductionEntryDebugScenarios
                        .AgeConditionActionId,
                    CharacterLifeCausalProductionEntryDebugScenarios
                        .LifeStageActionId
                },
                Array.Empty<string>(),
                CharacterLifeInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        CharacterLifeCausalProductionEntryDebugScenarios
                            .AgeConditionActionId,
                        CharacterLifeCausalProductionEntryDebugScenarios
                            .LifeStageActionId,
                        "observed-state",
                        "characterLife.stage",
                        "characterLife.chronologicalAgeDays",
                        "characterLife.biologicalAgeDayUnits",
                        "characterLife.conditionSeverity",
                        "characterLife.conditionNextProgressDayUnits",
                        "characterLife.ageConditionOutcomeCount",
                        "characterLife.exactOutcomeCount")
                }),
            Template(
                "character-age-condition-episode",
                "single-persistent-character-life-owner",
                new[]
                {
                    CharacterLifeCausalProductionEntryDebugScenarios
                        .AgeConditionActionId
                },
                Array.Empty<string>(),
                CharacterLifeInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "character-life-stage-episode",
                "single-persistent-character-life-owner",
                new[]
                {
                    CharacterLifeCausalProductionEntryDebugScenarios
                        .LifeStageActionId
                },
                Array.Empty<string>(),
                CharacterLifeInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "faction-route-progression-sequence",
                "single-persistent-faction-route-owner",
                new[]
                {
                    FactionRouteCausalProductionEntryDebugScenarios
                        .SettlementActionId,
                    FactionRouteCausalProductionEntryDebugScenarios
                        .ArrivalActionId,
                    FactionRouteCausalProductionEntryDebugScenarios
                        .CargoActionId
                },
                Array.Empty<string>(),
                FactionRouteInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        FactionRouteCausalProductionEntryDebugScenarios
                            .SettlementActionId,
                        FactionRouteCausalProductionEntryDebugScenarios
                            .ArrivalActionId,
                        "observed-state",
                        "factionRoute.stage",
                        "factionRoute.routeId",
                        "factionRoute.routeStatus",
                        "factionRoute.settlementState",
                        "factionRoute.sessionMoney",
                        "factionRoute.settlementOutcomeCount",
                        "factionRoute.exactOutcomeCount"),
                    Edge(
                        FactionRouteCausalProductionEntryDebugScenarios
                            .ArrivalActionId,
                        FactionRouteCausalProductionEntryDebugScenarios
                            .CargoActionId,
                        "observed-state",
                        "factionRoute.stage",
                        "factionRoute.routeStatus",
                        "factionRoute.pathIndex",
                        "factionRoute.cargoDeliveryState",
                        "factionRoute.arrivalOutcomeCount",
                        "factionRoute.exactOutcomeCount")
                }),
            Template(
                "faction-route-settlement-episode",
                "single-persistent-faction-route-owner",
                new[]
                {
                    FactionRouteCausalProductionEntryDebugScenarios
                        .SettlementActionId
                },
                Array.Empty<string>(),
                FactionRouteInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "faction-route-arrival-episode",
                "single-persistent-faction-route-owner",
                new[]
                {
                    FactionRouteCausalProductionEntryDebugScenarios
                        .ArrivalActionId
                },
                Array.Empty<string>(),
                FactionRouteInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "faction-route-cargo-episode",
                "single-persistent-faction-route-owner",
                new[]
                {
                    FactionRouteCausalProductionEntryDebugScenarios
                        .CargoActionId
                },
                Array.Empty<string>(),
                FactionRouteInitialPredicates(stage: 2),
                Array.Empty<JObject>()),
            Template(
                "character-consumables-progression-sequence",
                "single-persistent-character-consumables-owner",
                new[]
                {
                    ConsumablesProgressionCausalProductionEntryDebugScenarios
                        .MealActionId,
                    ConsumablesProgressionCausalProductionEntryDebugScenarios
                        .SubstanceActionId
                },
                Array.Empty<string>(),
                ConsumablesInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        ConsumablesProgressionCausalProductionEntryDebugScenarios
                            .MealActionId,
                        ConsumablesProgressionCausalProductionEntryDebugScenarios
                            .SubstanceActionId,
                        "observed-state",
                        "consumables.stage",
                        "consumables.completedOperationCount",
                        "consumables.hungerRecovered",
                        "consumables.moodApplied",
                        "consumables.acknowledgementCount",
                        "consumables.mealOutcomeCount",
                        "consumables.exactOutcomeCount")
                }),
            Template(
                "character-consumables-meal-episode",
                "single-persistent-character-consumables-owner",
                new[]
                {
                    ConsumablesProgressionCausalProductionEntryDebugScenarios
                        .MealActionId
                },
                Array.Empty<string>(),
                ConsumablesInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "character-consumables-substance-episode",
                "single-persistent-character-consumables-owner",
                new[]
                {
                    ConsumablesProgressionCausalProductionEntryDebugScenarios
                        .SubstanceActionId
                },
                Array.Empty<string>(),
                ConsumablesInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "survival-meal-shortage-sequence",
                "single-persistent-survival-food-owner",
                new[]
                {
                    MealShortageCausalProductionEntryDebugScenarios
                        .FirstMissActionId,
                    MealShortageCausalProductionEntryDebugScenarios
                        .RepeatMissActionId
                },
                Array.Empty<string>(),
                MealShortageInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        MealShortageCausalProductionEntryDebugScenarios
                            .FirstMissActionId,
                        MealShortageCausalProductionEntryDebugScenarios
                            .RepeatMissActionId,
                        "observed-state",
                        "mealShortage.stage",
                        "mealShortage.lastProcessedDay",
                        "mealShortage.missingFood",
                        "mealShortage.missingWater",
                        "mealShortage.outcomeCount",
                        "mealShortage.exactOutcomeCount")
                }),
            Template(
                "survival-meal-shortage-first-episode",
                "single-persistent-survival-food-owner",
                new[]
                {
                    MealShortageCausalProductionEntryDebugScenarios
                        .FirstMissActionId
                },
                Array.Empty<string>(),
                MealShortageInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "survival-meal-shortage-repeat-episode",
                "single-persistent-survival-food-owner",
                new[]
                {
                    MealShortageCausalProductionEntryDebugScenarios
                        .RepeatMissActionId
                },
                Array.Empty<string>(),
                MealShortageInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "survival-primitive-rest-sequence",
                "single-persistent-primitive-survival-owner",
                new[]
                {
                    PrimitiveSurvivalCausalProductionEntryDebugScenarios
                        .FirstRestActionId,
                    PrimitiveSurvivalCausalProductionEntryDebugScenarios
                        .RepeatRestActionId
                },
                Array.Empty<string>(),
                PrimitiveSurvivalInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        PrimitiveSurvivalCausalProductionEntryDebugScenarios
                            .FirstRestActionId,
                        PrimitiveSurvivalCausalProductionEntryDebugScenarios
                            .RepeatRestActionId,
                        "observed-state",
                        "primitiveSurvival.stage",
                        "primitiveSurvival.calendarDay",
                        "primitiveSurvival.sleep",
                        "primitiveSurvival.hygiene",
                        "primitiveSurvival.floorRestMood",
                        "primitiveSurvival.outcomeOwnerRevision",
                        "primitiveSurvival.exactOutcomeCount")
                }),
            Template(
                "survival-primitive-rest-first-episode",
                "single-persistent-primitive-survival-owner",
                new[]
                {
                    PrimitiveSurvivalCausalProductionEntryDebugScenarios
                        .FirstRestActionId
                },
                Array.Empty<string>(),
                PrimitiveSurvivalInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "survival-primitive-rest-repeat-episode",
                "single-persistent-primitive-survival-owner",
                new[]
                {
                    PrimitiveSurvivalCausalProductionEntryDebugScenarios
                        .RepeatRestActionId
                },
                Array.Empty<string>(),
                PrimitiveSurvivalInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "service-hub-day-sequence",
                "single-persistent-service-hub-day-owner",
                new[]
                {
                    ServiceHubDayCausalProductionEntryDebugScenarios.VisitActionId,
                    ServiceHubDayCausalProductionEntryDebugScenarios.ModeActionId,
                    ServiceHubDayCausalProductionEntryDebugScenarios.CloseDayActionId
                },
                Array.Empty<string>(),
                ServiceHubDayInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        ServiceHubDayCausalProductionEntryDebugScenarios.VisitActionId,
                        ServiceHubDayCausalProductionEntryDebugScenarios.ModeActionId,
                        "observed-state",
                        "serviceHubDay.stage",
                        "serviceHubDay.completedUses",
                        "serviceHubDay.settlementVisits",
                        "serviceHubDay.visitOwnerRevision",
                        "serviceHubDay.exactOutcomeCount"),
                    Edge(
                        ServiceHubDayCausalProductionEntryDebugScenarios.ModeActionId,
                        ServiceHubDayCausalProductionEntryDebugScenarios.CloseDayActionId,
                        "observed-state",
                        "serviceHubDay.stage",
                        "serviceHubDay.serviceMode",
                        "serviceHubDay.settlementVisits",
                        "serviceHubDay.modeOwnerRevision",
                        "serviceHubDay.exactOutcomeCount")
                }),
            Template(
                "service-hub-visit-episode",
                "single-persistent-service-hub-day-owner",
                new[]
                {
                    ServiceHubDayCausalProductionEntryDebugScenarios.VisitActionId
                },
                Array.Empty<string>(),
                ServiceHubDayInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "service-hub-mode-episode",
                "single-persistent-service-hub-day-owner",
                new[]
                {
                    ServiceHubDayCausalProductionEntryDebugScenarios.ModeActionId
                },
                Array.Empty<string>(),
                ServiceHubDayInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "service-hub-close-day-episode",
                "single-persistent-service-hub-day-owner",
                new[]
                {
                    ServiceHubDayCausalProductionEntryDebugScenarios.CloseDayActionId
                },
                Array.Empty<string>(),
                ServiceHubDayInitialPredicates(stage: 2),
                Array.Empty<JObject>()),
            Template(
                "invasion-lifecycle-sequence",
                "single-persistent-invasion-lifecycle-owner",
                new[]
                {
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionStartActionId,
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionBreachActionId,
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionDamageActionId,
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionFrontCollapseActionId,
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionResolveActionId
                },
                Array.Empty<string>(),
                InvasionLifecycleInitialPredicates(stage: 0),
                new[]
                {
                    Edge(
                        MigratedProducerCombatDefenseEntryE2EDebugScenarios
                            .InvasionStartActionId,
                        MigratedProducerCombatDefenseEntryE2EDebugScenarios
                            .InvasionBreachActionId,
                        "observed-state",
                        "invasionLifecycle.stage",
                        "invasionLifecycle.activeIntruders",
                        "invasionLifecycle.startOwnerRevision",
                        "invasionLifecycle.exactOutcomeCount"),
                    Edge(
                        MigratedProducerCombatDefenseEntryE2EDebugScenarios
                            .InvasionBreachActionId,
                        MigratedProducerCombatDefenseEntryE2EDebugScenarios
                            .InvasionDamageActionId,
                        "observed-state",
                        "invasionLifecycle.stage",
                        "invasionLifecycle.hasBreachedDungeon",
                        "invasionLifecycle.breachOwnerRevision",
                        "invasionLifecycle.exactOutcomeCount"),
                    Edge(
                        MigratedProducerCombatDefenseEntryE2EDebugScenarios
                            .InvasionDamageActionId,
                        MigratedProducerCombatDefenseEntryE2EDebugScenarios
                            .InvasionFrontCollapseActionId,
                        "observed-state",
                        "invasionLifecycle.stage",
                        "invasionLifecycle.facilityDamaged",
                        "invasionLifecycle.damageOwnerRevision",
                        "invasionLifecycle.exactOutcomeCount"),
                    Edge(
                        MigratedProducerCombatDefenseEntryE2EDebugScenarios
                            .InvasionFrontCollapseActionId,
                        MigratedProducerCombatDefenseEntryE2EDebugScenarios
                            .InvasionResolveActionId,
                        "observed-state",
                        "invasionLifecycle.stage",
                        "invasionLifecycle.frontCollapsed",
                        "invasionLifecycle.frontCollapseOwnerRevision",
                        "invasionLifecycle.exactOutcomeCount")
                }),
            Template(
                "invasion-lifecycle-start-episode",
                "single-persistent-invasion-lifecycle-owner",
                new[]
                {
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionStartActionId
                },
                Array.Empty<string>(),
                InvasionLifecycleInitialPredicates(stage: 0),
                Array.Empty<JObject>()),
            Template(
                "invasion-lifecycle-breach-episode",
                "single-persistent-invasion-lifecycle-owner",
                new[]
                {
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionBreachActionId
                },
                Array.Empty<string>(),
                InvasionLifecycleInitialPredicates(stage: 1),
                Array.Empty<JObject>()),
            Template(
                "invasion-lifecycle-damage-episode",
                "single-persistent-invasion-lifecycle-owner",
                new[]
                {
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionDamageActionId
                },
                Array.Empty<string>(),
                InvasionLifecycleInitialPredicates(stage: 2),
                Array.Empty<JObject>()),
            Template(
                "invasion-lifecycle-front-collapse-episode",
                "single-persistent-invasion-lifecycle-owner",
                new[]
                {
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionFrontCollapseActionId
                },
                Array.Empty<string>(),
                InvasionLifecycleInitialPredicates(stage: 3),
                Array.Empty<JObject>()),
            Template(
                "invasion-lifecycle-resolve-episode",
                "single-persistent-invasion-lifecycle-owner",
                new[]
                {
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionResolveActionId
                },
                Array.Empty<string>(),
                InvasionLifecycleInitialPredicates(stage: 4),
                Array.Empty<JObject>()),
            Template(
                "acquired-trait-manifestation-memory-erasure",
                "single-persistent-acquired-trait-character",
                new[]
                {
                    AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                        .ManifestActionId,
                    AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                        .ReactionActionId,
                    AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                        .EraseActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "trait.manifested", "equal", false),
                    Predicate("initial", "trait.reactionExecuted", "equal", false),
                    Predicate("initial", "trait.erased", "equal", false),
                    Predicate("initial", "trait.sealQuantity", "equal", 1)
                },
                new[]
                {
                    Edge(
                        AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                            .ManifestActionId,
                        AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                            .ReactionActionId,
                        "observed-state",
                        "trait.revision",
                        "trait.activeCount",
                        "trait.instanceId"),
                    Edge(
                        AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                            .ReactionActionId,
                        AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                            .EraseActionId,
                        "observed-state",
                        "trait.revision",
                        "trait.activeCount",
                        "trait.narrativeFactCount",
                        "trait.sealQuantity",
                        "trait.instanceId")
                }),
            Template(
                "acquired-trait-manifestation-episode",
                "single-persistent-acquired-trait-character",
                new[]
                {
                    AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                        .ManifestActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "trait.manifested", "equal", false),
                    Predicate("initial", "trait.reactionExecuted", "equal", false),
                    Predicate("initial", "trait.erased", "equal", false),
                    Predicate("initial", "trait.sealQuantity", "equal", 1)
                },
                Array.Empty<JObject>()),
            Template(
                "acquired-trait-special-reaction-episode",
                "single-persistent-acquired-trait-character",
                new[]
                {
                    AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                        .ReactionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "trait.manifested", "equal", true),
                    Predicate("initial", "trait.reactionExecuted", "equal", false),
                    Predicate("initial", "trait.erased", "equal", false),
                    Predicate("initial", "trait.activeCount", "equal", 1)
                },
                Array.Empty<JObject>()),
            Template(
                "acquired-trait-memory-erasure-episode",
                "single-persistent-acquired-trait-character",
                new[]
                {
                    AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                        .EraseActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "trait.manifested", "equal", true),
                    Predicate("initial", "trait.reactionExecuted", "equal", true),
                    Predicate("initial", "trait.erased", "equal", false),
                    Predicate("initial", "trait.activeCount", "equal", 1),
                    Predicate("initial", "trait.sealQuantity", "equal", 1)
                },
                Array.Empty<JObject>()),
            Template(
                "captivity-interaction-episode",
                "single-persistent-captivity-owner",
                new[]
                {
                    CaptivityCausalProductionEntryDebugScenarios.InteractionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "captivity.interactionResolved", "equal", false),
                    Predicate("initial", "captivity.milestoneUnlocked", "equal", false),
                    Predicate("initial", "captivity.ransomResolved", "equal", false),
                    Predicate("initial", "captivity.escaped", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "captivity-performer-milestone-episode",
                "single-persistent-captivity-owner",
                new[]
                {
                    CaptivityCausalProductionEntryDebugScenarios.MilestoneActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "captivity.interactionResolved", "equal", true),
                    Predicate("initial", "captivity.milestoneUnlocked", "equal", false),
                    Predicate("initial", "captivity.ransomResolved", "equal", false),
                    Predicate("initial", "captivity.escaped", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "captivity-ransom-terminal-episode",
                "single-persistent-captivity-owner",
                new[]
                {
                    CaptivityCausalProductionEntryDebugScenarios.RansomActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "captivity.interactionResolved", "equal", true),
                    Predicate("initial", "captivity.milestoneUnlocked", "equal", true),
                    Predicate("initial", "captivity.ransomResolved", "equal", false),
                    Predicate("initial", "captivity.escaped", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "captivity-escape-terminal-episode",
                "single-persistent-captivity-owner",
                new[]
                {
                    CaptivityCausalProductionEntryDebugScenarios.EscapeActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "captivity.interactionResolved", "equal", true),
                    Predicate("initial", "captivity.milestoneUnlocked", "equal", true),
                    Predicate("initial", "captivity.ransomResolved", "equal", false),
                    Predicate("initial", "captivity.escaped", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "equipment-apparel-craft-equip",
                "single-persistent-crafted-apparel",
                new[]
                {
                    EquipmentApparelCausalProductionEntryDebugScenarios.CraftActionId,
                    EquipmentApparelCausalProductionEntryDebugScenarios.EquipActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "equipment.materialQuantity", "equal", 4),
                    Predicate("initial", "equipment.crafted", "equal", false),
                    Predicate("initial", "equipment.equipped", "equal", false)
                },
                new[]
                {
                    Edge(
                        EquipmentApparelCausalProductionEntryDebugScenarios.CraftActionId,
                        EquipmentApparelCausalProductionEntryDebugScenarios.EquipActionId,
                        "observed-state",
                        "equipment.materialQuantity",
                        "equipment.outputQuantity",
                        "equipment.outputStackId",
                        "equipment.outputInstanceId",
                        "equipment.outputState",
                        "equipment.apparelDurability")
                }),
            Template(
                "equipment-apparel-craft-episode",
                "single-persistent-crafted-apparel",
                new[]
                {
                    EquipmentApparelCausalProductionEntryDebugScenarios.CraftActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "equipment.materialQuantity", "equal", 4),
                    Predicate("initial", "equipment.crafted", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "equipment-apparel-equip-episode",
                "single-persistent-crafted-apparel",
                new[]
                {
                    EquipmentApparelCausalProductionEntryDebugScenarios.EquipActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "equipment.outputQuantity", "equal", 1),
                    Predicate("initial", "equipment.crafted", "equal", true),
                    Predicate("initial", "equipment.equipped", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "research-knowledge-progress-completion",
                "single-persistent-knowledge-research-task",
                new[]
                {
                    KnowledgeResearchOutcomeDebugScenarios
                        .CausalResearchProgressActionId,
                    KnowledgeResearchOutcomeDebugScenarios
                        .CausalResearchCompletionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "research.completedWork", "equal", 0f),
                    Predicate("initial", "research.sourceAvailable", "equal", true),
                    Predicate("initial", "research.completed", "equal", false)
                },
                new[]
                {
                    Edge(
                        KnowledgeResearchOutcomeDebugScenarios
                            .CausalResearchProgressActionId,
                        KnowledgeResearchOutcomeDebugScenarios
                            .CausalResearchCompletionActionId,
                        "observed-state",
                        "research.completedWork",
                        "research.outcomeSequence",
                        "research.sourceAvailable")
                }),
            Template(
                "research-knowledge-progress-episode",
                "single-persistent-knowledge-research-task",
                new[]
                {
                    KnowledgeResearchOutcomeDebugScenarios
                        .CausalResearchProgressActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "research.completedWork", "equal", 0f),
                    Predicate("initial", "research.sourceAvailable", "equal", true),
                    Predicate("initial", "research.completed", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "research-knowledge-completion-episode",
                "single-persistent-knowledge-research-task",
                new[]
                {
                    KnowledgeResearchOutcomeDebugScenarios
                        .CausalResearchCompletionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "research.completedWork", "equal", 8f),
                    Predicate("initial", "research.sourceAvailable", "equal", true),
                    Predicate("initial", "research.progressApplied", "equal", true),
                    Predicate("initial", "research.completed", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "production-bill-command-completion",
                "single-persistent-production-bill",
                new[]
                {
                    ProductionPreparedOutputFullPersistenceDebugScenarios
                        .CausalProductionCommandActionId,
                    ProductionPreparedOutputFullPersistenceDebugScenarios
                        .CausalProductionCompletionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "production.billCount", "equal", 0),
                    Predicate("initial", "production.commandOutcomeSequence", "equal", 1L),
                    Predicate("initial", "production.outputQuantity", "equal", 0),
                    Predicate("initial", "production.commandApplied", "equal", false),
                    Predicate("initial", "production.completed", "equal", false)
                },
                new[]
                {
                    Edge(
                        ProductionPreparedOutputFullPersistenceDebugScenarios
                            .CausalProductionCommandActionId,
                        ProductionPreparedOutputFullPersistenceDebugScenarios
                            .CausalProductionCompletionActionId,
                        "observed-state",
                        "production.billId",
                        "production.billCount",
                        "production.commandOutcomeSequence",
                        "production.cycleSequence",
                        "production.remainingCycles",
                        "production.commandApplied")
                }),
            Template(
                "production-bill-command-episode",
                "single-persistent-production-bill",
                new[]
                {
                    ProductionPreparedOutputFullPersistenceDebugScenarios
                        .CausalProductionCommandActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "production.billCount", "equal", 0),
                    Predicate("initial", "production.commandOutcomeSequence", "equal", 1L),
                    Predicate("initial", "production.commandApplied", "equal", false),
                    Predicate("initial", "production.completed", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "production-bill-completion-episode",
                "single-persistent-production-bill",
                new[]
                {
                    ProductionPreparedOutputFullPersistenceDebugScenarios
                        .CausalProductionCompletionActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "production.billCount", "equal", 1),
                    Predicate("initial", "production.commandOutcomeSequence", "equal", 2L),
                    Predicate("initial", "production.cycleSequence", "equal", 1),
                    Predicate("initial", "production.outputQuantity", "equal", 0),
                    Predicate("initial", "production.commandApplied", "equal", true),
                    Predicate("initial", "production.completed", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "facility-evolution-two-stage",
                "single-persistent-facility-evolution-lineage",
                new[]
                {
                    FacilityEvolutionDebugScenarios
                        .CausalFacilityEvolutionFirstActionId,
                    FacilityEvolutionDebugScenarios
                        .CausalFacilityEvolutionSecondActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "facility.starGrade", "equal", 1),
                    Predicate("initial", "facility.historyCount", "equal", 0),
                    Predicate("initial", "facility.evolutionStage", "equal", 0),
                    Predicate("initial", "facility.firstCompleted", "equal", false),
                    Predicate("initial", "facility.secondCompleted", "equal", false)
                },
                new[]
                {
                    Edge(
                        FacilityEvolutionDebugScenarios
                            .CausalFacilityEvolutionFirstActionId,
                        FacilityEvolutionDebugScenarios
                            .CausalFacilityEvolutionSecondActionId,
                        "observed-state",
                        "facility.definitionId",
                        "facility.starGrade",
                        "facility.historyCount",
                        "facility.generation",
                        "facility.exactOutcomeCount",
                        "facility.evolutionStage",
                        "facility.firstCompleted")
                }),
            Template(
                "facility-evolution-stage-one-episode",
                "single-persistent-facility-evolution-lineage",
                new[]
                {
                    FacilityEvolutionDebugScenarios
                        .CausalFacilityEvolutionFirstActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "facility.starGrade", "equal", 1),
                    Predicate("initial", "facility.historyCount", "equal", 0),
                    Predicate("initial", "facility.evolutionStage", "equal", 0),
                    Predicate("initial", "facility.firstCompleted", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "facility-evolution-stage-two-episode",
                "single-persistent-facility-evolution-lineage",
                new[]
                {
                    FacilityEvolutionDebugScenarios
                        .CausalFacilityEvolutionSecondActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "facility.starGrade", "equal", 2),
                    Predicate("initial", "facility.historyCount", "equal", 1),
                    Predicate("initial", "facility.exactOutcomeCount", "equal", 1),
                    Predicate("initial", "facility.evolutionStage", "equal", 1),
                    Predicate("initial", "facility.firstCompleted", "equal", true),
                    Predicate("initial", "facility.secondCompleted", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "facility-synthesis-two-stage",
                "single-persistent-facility-synthesis-lineage",
                new[]
                {
                    FacilitySynthesisDebugScenarios
                        .CausalSynthesisFirstActionId,
                    FacilitySynthesisDebugScenarios
                        .CausalSynthesisSecondActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "facilitySynthesis.stage", "equal", 0),
                    Predicate("initial", "facilitySynthesis.outcomeRevision", "equal", 0L),
                    Predicate("initial", "facilitySynthesis.liveBuildingCount", "equal", 3),
                    Predicate("initial", "facilitySynthesis.exactOutcomeCount", "equal", 0),
                    Predicate("initial", "facilitySynthesis.battleBannerUnlocked", "equal", true),
                    Predicate("initial", "facilitySynthesis.patrolBoardCompleted", "equal", false),
                    Predicate("initial", "facilitySynthesis.battleBannerCompleted", "equal", false)
                },
                new[]
                {
                    Edge(
                        FacilitySynthesisDebugScenarios
                            .CausalSynthesisFirstActionId,
                        FacilitySynthesisDebugScenarios
                            .CausalSynthesisSecondActionId,
                        "observed-state",
                        "facilitySynthesis.survivorId",
                        "facilitySynthesis.definitionId",
                        "facilitySynthesis.outcomeRevision",
                        "facilitySynthesis.liveBuildingCount",
                        "facilitySynthesis.exactOutcomeCount",
                        "facilitySynthesis.patrolBoardCompleted")
                }),
            Template(
                "facility-synthesis-patrol-board-episode",
                "single-persistent-facility-synthesis-lineage",
                new[]
                {
                    FacilitySynthesisDebugScenarios
                        .CausalSynthesisFirstActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "facilitySynthesis.stage", "equal", 0),
                    Predicate("initial", "facilitySynthesis.outcomeRevision", "equal", 0L),
                    Predicate("initial", "facilitySynthesis.liveBuildingCount", "equal", 3),
                    Predicate("initial", "facilitySynthesis.exactOutcomeCount", "equal", 0),
                    Predicate("initial", "facilitySynthesis.battleBannerUnlocked", "equal", true),
                    Predicate("initial", "facilitySynthesis.patrolBoardCompleted", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "facility-synthesis-battle-banner-episode",
                "single-persistent-facility-synthesis-lineage",
                new[]
                {
                    FacilitySynthesisDebugScenarios
                        .CausalSynthesisSecondActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "facilitySynthesis.stage", "equal", 1),
                    Predicate("initial", "facilitySynthesis.outcomeRevision", "equal", 1L),
                    Predicate("initial", "facilitySynthesis.liveBuildingCount", "equal", 2),
                    Predicate("initial", "facilitySynthesis.exactOutcomeCount", "equal", 1),
                    Predicate("initial", "facilitySynthesis.battleBannerUnlocked", "equal", true),
                    Predicate("initial", "facilitySynthesis.patrolBoardCompleted", "equal", true),
                    Predicate("initial", "facilitySynthesis.battleBannerCompleted", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "infrastructure-power-disconnect-reconnect",
                "single-persistent-power-connection",
                new[]
                {
                    InfrastructureCommandCausalProductionEntryDebugScenarios
                        .DisconnectActionId,
                    InfrastructureCommandCausalProductionEntryDebugScenarios
                        .ReconnectActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "infrastructure.connectionEnabled", "equal", true),
                    Predicate("initial", "infrastructure.commandNextOwnerRevision", "equal", 1L),
                    Predicate("initial", "infrastructure.exactOutcomeCount", "equal", 0),
                    Predicate("initial", "infrastructure.stage", "equal", 0),
                    Predicate("initial", "infrastructure.disconnected", "equal", false),
                    Predicate("initial", "infrastructure.reconnected", "equal", false)
                },
                new[]
                {
                    Edge(
                        InfrastructureCommandCausalProductionEntryDebugScenarios
                            .DisconnectActionId,
                        InfrastructureCommandCausalProductionEntryDebugScenarios
                            .ReconnectActionId,
                        "observed-state",
                        "infrastructure.connectionEnabled",
                        "infrastructure.commandNextOwnerRevision",
                        "infrastructure.exactOutcomeCount",
                        "infrastructure.stage",
                        "infrastructure.disconnected")
                }),
            Template(
                "infrastructure-power-disconnect-episode",
                "single-persistent-power-connection",
                new[]
                {
                    InfrastructureCommandCausalProductionEntryDebugScenarios
                        .DisconnectActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "infrastructure.connectionEnabled", "equal", true),
                    Predicate("initial", "infrastructure.commandNextOwnerRevision", "equal", 1L),
                    Predicate("initial", "infrastructure.exactOutcomeCount", "equal", 0),
                    Predicate("initial", "infrastructure.stage", "equal", 0),
                    Predicate("initial", "infrastructure.disconnected", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "infrastructure-power-reconnect-episode",
                "single-persistent-power-connection",
                new[]
                {
                    InfrastructureCommandCausalProductionEntryDebugScenarios
                        .ReconnectActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "infrastructure.connectionEnabled", "equal", false),
                    Predicate("initial", "infrastructure.commandNextOwnerRevision", "equal", 2L),
                    Predicate("initial", "infrastructure.exactOutcomeCount", "equal", 1),
                    Predicate("initial", "infrastructure.stage", "equal", 1),
                    Predicate("initial", "infrastructure.disconnected", "equal", true),
                    Predicate("initial", "infrastructure.reconnected", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "dungeon-space-quarry-stonecutting",
                "single-persistent-dungeon-space",
                new[]
                {
                    DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                        .QuarryActionId,
                    DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                        .StonecuttingActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "dungeonSpace.stage", "equal", 0),
                    Predicate("initial", "dungeonSpace.tier", "equal", 0),
                    Predicate("initial", "dungeonSpace.interiorColumns", "equal", 29),
                    Predicate("initial", "dungeonSpace.gridWidth", "equal", 60),
                    Predicate("initial", "dungeonSpace.exactOutcomeCount", "equal", 0),
                    Predicate("initial", "dungeonSpace.quarryCompleted", "equal", false),
                    Predicate("initial", "dungeonSpace.stonecuttingCompleted", "equal", false)
                },
                new[]
                {
                    Edge(
                        DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                            .QuarryActionId,
                        DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                            .StonecuttingActionId,
                        "observed-state",
                        "dungeonSpace.stage",
                        "dungeonSpace.tier",
                        "dungeonSpace.interiorColumns",
                        "dungeonSpace.gridWidth",
                        "dungeonSpace.exactOutcomeCount",
                        "dungeonSpace.quarryCompleted")
                }),
            Template(
                "dungeon-space-quarry-episode",
                "single-persistent-dungeon-space",
                new[]
                {
                    DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                        .QuarryActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "dungeonSpace.stage", "equal", 0),
                    Predicate("initial", "dungeonSpace.tier", "equal", 0),
                    Predicate("initial", "dungeonSpace.interiorColumns", "equal", 29),
                    Predicate("initial", "dungeonSpace.gridWidth", "equal", 60),
                    Predicate("initial", "dungeonSpace.exactOutcomeCount", "equal", 0),
                    Predicate("initial", "dungeonSpace.quarryCompleted", "equal", false)
                },
                Array.Empty<JObject>()),
            Template(
                "dungeon-space-stonecutting-episode",
                "single-persistent-dungeon-space",
                new[]
                {
                    DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                        .StonecuttingActionId
                },
                Array.Empty<string>(),
                new[]
                {
                    Predicate("initial", "dungeonSpace.stage", "equal", 1),
                    Predicate("initial", "dungeonSpace.tier", "equal", 1),
                    Predicate("initial", "dungeonSpace.interiorColumns", "equal", 51),
                    Predicate("initial", "dungeonSpace.gridWidth", "equal", 68),
                    Predicate("initial", "dungeonSpace.exactOutcomeCount", "equal", 1),
                    Predicate("initial", "dungeonSpace.quarryCompleted", "equal", true),
                    Predicate("initial", "dungeonSpace.stonecuttingCompleted", "equal", false)
                },
                Array.Empty<JObject>())
        }
    };

    private static JObject SubjectRole(
        string roleId,
        string kind,
        string binding,
        string profile) => new()
    {
        ["roleId"] = roleId,
        ["kind"] = kind,
        ["binding"] = binding,
        ["profile"] = profile
    };

    private static JObject StateKey(string id, string type) => new()
    {
        ["id"] = id,
        ["type"] = type,
        ["authority"] = "observed-from-gameplay-owner"
    };

    private static string[] CaptivityStateKeys() => new[]
    {
        "captivity.ownerId", "captivity.captiveId", "captivity.status",
        "captivity.will", "captivity.fear", "captivity.trust",
        "captivity.grudge", "captivity.performerFame",
        "captivity.performerSkill", "captivity.privilegeTier",
        "captivity.carePriorityUnlocked",
        "captivity.interactionOutcomeRevision",
        "captivity.performerMilestoneOutcomeRevision",
        "captivity.ransomOutcomeRevision", "captivity.escapeOutcomeRevision",
        "captivity.retaliationPressure", "captivity.ransomAmount",
        "captivity.interactionResolved", "captivity.milestoneUnlocked",
        "captivity.ransomResolved", "captivity.escaped"
    };

    private static string[] TradeInventoryStateKeys() => new[]
    {
        "tradeInventory.ownerId", "tradeInventory.stage",
        "tradeInventory.absoluteDay", "tradeInventory.itemId",
        "tradeInventory.sourceStackId", "tradeInventory.relocatedStackId",
        "tradeInventory.sourceQuantity", "tradeInventory.relocatedQuantity",
        "tradeInventory.stackCount", "tradeInventory.relocatedPresent",
        "tradeInventory.sourceX", "tradeInventory.sourceY",
        "tradeInventory.destinationX", "tradeInventory.destinationY",
        "tradeInventory.wasteOrigin", "tradeInventory.wasteDisposition",
        "tradeInventory.wasteEnabled",
        "tradeInventory.wasteMaximumPercent",
        "tradeInventory.wasteNextOutcomeSequence",
        "tradeInventory.relocationOutcomeCount",
        "tradeInventory.dispositionOutcomeCount",
        "tradeInventory.wastePolicyOutcomeCount",
        "tradeInventory.exactOutcomeCount"
    };

    private static string[] DiseaseRouteStateKeys() => new[]
    {
        "diseaseRoute.ownerId", "diseaseRoute.stage",
        "diseaseRoute.calendarDay", "diseaseRoute.lastPublishedAbsoluteDay",
        "diseaseRoute.actorId", "diseaseRoute.vectorId",
        "diseaseRoute.diseaseId", "diseaseRoute.healthCurrentAbsoluteDay",
        "diseaseRoute.healthVersion", "diseaseRoute.pendingExposureCount",
        "diseaseRoute.weightedExposureHours", "diseaseRoute.exactOutcomeCount",
        "diseaseRoute.lastOutcomeOperationId",
        "diseaseRoute.lastOutcomeCommitRevision",
        "diseaseRoute.lastOutcomeOwnerRevision"
    };

    private static string[] VisitorCheckoutStateKeys() => new[]
    {
        "visitorCheckout.ownerId", "visitorCheckout.stage", "visitorCheckout.absoluteDay",
        "visitorCheckout.actorId", "visitorCheckout.actorName", "visitorCheckout.workerId",
        "visitorCheckout.shopId", "visitorCheckout.incidentId", "visitorCheckout.receptionApplied",
        "visitorCheckout.receptionMood", "visitorCheckout.receptionRemainingSeconds",
        "visitorCheckout.incidentRemainingSeconds", "visitorCheckout.incidentProgress",
        "visitorCheckout.stockQuantity", "visitorCheckout.completedUses", "visitorCheckout.holdingMoney",
        "visitorCheckout.currentUsers", "visitorCheckout.crimeDraws", "visitorCheckout.crimeChance",
        "visitorCheckout.crimeRoll", "visitorCheckout.exactOutcomeCount",
        "visitorCheckout.clockTime", "visitorCheckout.clockFrame"
    };

    private static JObject[] VisitorCheckoutInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(stage));
        return new[]
        {
            Predicate("initial", "visitorCheckout.stage", "equal", stage),
            Predicate("initial", "visitorCheckout.absoluteDay", "equal", 7),
            Predicate("initial", "visitorCheckout.actorName", "equal", "Slime"),
            Predicate("initial", "visitorCheckout.incidentId", "equal", "incident:Informant:1"),
            Predicate("initial", "visitorCheckout.receptionApplied", "equal", stage == 1),
            Predicate("initial", "visitorCheckout.receptionMood", "equal", stage == 0 ? 0d
                : VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.StateNumber(2.65f)),
            Predicate("initial", "visitorCheckout.receptionRemainingSeconds", "equal", stage * 180d),
            Predicate("initial", "visitorCheckout.incidentRemainingSeconds", "equal", 150d),
            Predicate("initial", "visitorCheckout.incidentProgress", "equal", stage * 16d),
            Predicate("initial", "visitorCheckout.stockQuantity", "equal", 1),
            Predicate("initial", "visitorCheckout.completedUses", "equal", 0),
            Predicate("initial", "visitorCheckout.holdingMoney", "equal", 500),
            Predicate("initial", "visitorCheckout.currentUsers", "equal", 0),
            Predicate("initial", "visitorCheckout.crimeDraws", "equal", 0),
            Predicate("initial", "visitorCheckout.crimeChance", "equal", 0d),
            Predicate("initial", "visitorCheckout.crimeRoll", "equal", -1d),
            Predicate("initial", "visitorCheckout.exactOutcomeCount", "equal", stage),
            Predicate("initial", "visitorCheckout.clockTime", "equal", 0d),
            Predicate("initial", "visitorCheckout.clockFrame", "equal", 0)
        };
    }

    private static string[] OwnerRunCompletionStateKeys() => new[]
    {
        "ownerRun.ownerId", "ownerRun.stage", "ownerRun.runSeed", "ownerRun.absoluteDay",
        "ownerRun.actorId", "ownerRun.actorDataId", "ownerRun.actorName", "ownerRun.phase",
        "ownerRun.outcome", "ownerRun.ownerEnded", "ownerRun.settlementEnded",
        "ownerRun.completedRunCount", "ownerRun.earnedCurrency", "ownerRun.spentCurrency",
        "ownerRun.availableCurrency", "ownerRun.upgradeId", "ownerRun.upgradeCost",
        "ownerRun.upgradeLevel", "ownerRun.facilityCandidateBonus", "ownerRun.ownerOutcomeCount",
        "ownerRun.settlementOutcomeCount", "ownerRun.upgradeOutcomeCount", "ownerRun.exactOutcomeCount"
    };

    private static JObject[] OwnerRunCompletionInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(stage));
        return new[]
        {
            Predicate("initial", "ownerRun.stage", "equal", stage),
            Predicate("initial", "ownerRun.runSeed", "equal", 104729),
            Predicate("initial", "ownerRun.absoluteDay", "equal", 3),
            Predicate("initial", "ownerRun.actorId", "equal", "owner"),
            Predicate("initial", "ownerRun.actorDataId", "equal", 1001),
            Predicate("initial", "ownerRun.actorName", "equal", "슬라임 사장"),
            Predicate("initial", "ownerRun.phase", "equal", stage == 0 ? "Preparation" : "Finished"),
            Predicate("initial", "ownerRun.outcome", "equal", stage == 0 ? "None" : "Victory"),
            Predicate("initial", "ownerRun.ownerEnded", "equal", stage == 1),
            Predicate("initial", "ownerRun.settlementEnded", "equal", stage == 1),
            Predicate("initial", "ownerRun.completedRunCount", "equal", stage),
            Predicate("initial", "ownerRun.earnedCurrency", "equal", stage * 105),
            Predicate("initial", "ownerRun.spentCurrency", "equal", 0),
            Predicate("initial", "ownerRun.availableCurrency", "equal", stage * 105),
            Predicate("initial", "ownerRun.upgradeId", "equal", "meta:starting-facility-candidate"),
            Predicate("initial", "ownerRun.upgradeCost", "equal", 80),
            Predicate("initial", "ownerRun.upgradeLevel", "equal", 0),
            Predicate("initial", "ownerRun.facilityCandidateBonus", "equal", 0),
            Predicate("initial", "ownerRun.ownerOutcomeCount", "equal", stage),
            Predicate("initial", "ownerRun.settlementOutcomeCount", "equal", stage),
            Predicate("initial", "ownerRun.upgradeOutcomeCount", "equal", 0),
            Predicate("initial", "ownerRun.exactOutcomeCount", "equal", stage * 2)
        };
    }

    private static string[] OffenseNodeResolutionStateKeys() => new[]
    {
        "offenseNode.ownerId", "offenseNode.stage",
        "offenseNode.absoluteDay", "offenseNode.expeditionId",
        "offenseNode.actorId", "offenseNode.targetKnown",
        "offenseNode.currentNodeId", "offenseNode.completedNodeCount",
        "offenseNode.light", "offenseNode.rations", "offenseNode.tools",
        "offenseNode.consumedRations", "offenseNode.consumedTools",
        "offenseNode.generalLoot", "offenseNode.foodLoot",
        "offenseNode.actorLevel", "offenseNode.actorExperience",
        "offenseNode.actorStress", "offenseNode.actorNarrativeCount",
        "offenseNode.exactOutcomeCount"
    };

    private static JObject[] OffenseNodeResolutionInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        bool approachResolved = stage == 1;
        return new[]
        {
            Predicate("initial", "offenseNode.stage", "equal", stage),
            Predicate("initial", "offenseNode.absoluteDay", "equal", 46),
            Predicate("initial", "offenseNode.expeditionId", "equal",
                "expedition:causal-node:farm"),
            Predicate("initial", "offenseNode.actorId", "equal",
                "character:causal-node:scout"),
            Predicate("initial", "offenseNode.targetKnown", "equal", true),
            Predicate("initial", "offenseNode.currentNodeId", "equal",
                approachResolved ? "food_farm:cache" : "food_farm:approach-event"),
            Predicate("initial", "offenseNode.completedNodeCount", "equal",
                stage + 1),
            Predicate("initial", "offenseNode.light", "equal",
                approachResolved ? 3.25f : 24.25f),
            Predicate("initial", "offenseNode.rations", "equal",
                approachResolved ? 2 : 3),
            Predicate("initial", "offenseNode.tools", "equal",
                approachResolved ? 0 : 1),
            Predicate("initial", "offenseNode.consumedRations", "equal",
                approachResolved ? 2 : 1),
            Predicate("initial", "offenseNode.consumedTools", "equal",
                approachResolved ? 1 : 0),
            Predicate("initial", "offenseNode.generalLoot", "equal",
                approachResolved ? 4 : 0),
            Predicate("initial", "offenseNode.foodLoot", "equal", 0),
            Predicate("initial", "offenseNode.actorLevel", "equal",
                approachResolved ? 3 : 1),
            Predicate("initial", "offenseNode.actorExperience", "equal",
                approachResolved ? 5 : 0),
            Predicate("initial", "offenseNode.actorStress", "equal",
                approachResolved ? 4f : 0f),
            Predicate("initial", "offenseNode.actorNarrativeCount", "equal",
                stage),
            Predicate("initial", "offenseNode.exactOutcomeCount", "equal",
                stage)
        };
    }

    private static string[] OffenseTruthStateKeys() => new[]
    {
        "offenseTruth.ownerId", "offenseTruth.stage",
        "offenseTruth.absoluteDay", "offenseTruth.knownTargetCount",
        "offenseTruth.completedTargetCount",
        "offenseTruth.preliminaryKnown",
        "offenseTruth.preliminaryCompleted", "offenseTruth.truthKnown",
        "offenseTruth.truthCompleted", "offenseTruth.truthRevealed",
        "offenseTruth.revealedTargetId",
        "offenseTruth.migratedOwnerRevisionCount",
        "offenseTruth.preliminaryOutcomeCount",
        "offenseTruth.truthOutcomeCount",
        "offenseTruth.exactOutcomeCount"
    };

    private static string[] WorkIdentityStateKeys() => new[]
    {
        "workIdentity.ownerId", "workIdentity.characterId",
        "workIdentity.stage", "workIdentity.lastAppliedDay",
        "workIdentity.deliveryCursorCount",
        "workIdentity.deliveryOperationSequence",
        "workIdentity.identityRuleStateCount",
        "workIdentity.firstProcessRecorded", "workIdentity.moodValue",
        "workIdentity.moodFactorCount", "workIdentity.firstApplied",
        "workIdentity.secondApplied", "workIdentity.lastProductId",
        "workIdentity.exactOutcomeCount"
    };

    private static JObject[] WorkIdentityInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        return new[]
        {
            Predicate("initial", "workIdentity.stage", "equal", stage),
            Predicate("initial", "workIdentity.lastAppliedDay", "equal",
                stage == 0 ? -1 : 12),
            Predicate("initial", "workIdentity.deliveryCursorCount", "equal",
                stage == 0 ? 0 : 1),
            Predicate("initial", "workIdentity.deliveryOperationSequence", "equal",
                stage == 0 ? -1 : 0),
            Predicate("initial", "workIdentity.identityRuleStateCount", "equal",
                stage == 0 ? 0 : 2),
            Predicate("initial", "workIdentity.firstProcessRecorded", "equal",
                stage == 1),
            Predicate("initial", "workIdentity.firstApplied", "equal",
                stage == 1),
            Predicate("initial", "workIdentity.secondApplied", "equal", false),
            Predicate("initial", "workIdentity.lastProductId", "equal",
                stage == 0 ? "none" : "crop:causal-identity:first-harvest"),
            Predicate("initial", "workIdentity.exactOutcomeCount", "equal", stage)
        };
    }

    private static string[] TabooButcheryStateKeys() => new[]
    {
        "tabooButchery.ownerId", "tabooButchery.stage", "tabooButchery.absoluteDay",
        "tabooButchery.butcherId", "tabooButchery.butcherName", "tabooButchery.butcherSpecies",
        "tabooButchery.victimId", "tabooButchery.victimName", "tabooButchery.victimSpecies",
        "tabooButchery.victimHealth", "tabooButchery.victimDead",
        "tabooButchery.witnessId", "tabooButchery.witnessName", "tabooButchery.witnessSpecies",
        "tabooButchery.corpseStackId", "tabooButchery.corpseQuantity",
        "tabooButchery.corpseSourceId", "tabooButchery.corpseSourceName",
        "tabooButchery.corpseSourceSpecies", "tabooButchery.corpseDeathReason",
        "tabooButchery.corpseAuthorized", "tabooButchery.bloodAmount",
        "tabooButchery.meatQuantity", "tabooButchery.boneQuantity",
        "tabooButchery.butcherHygiene", "tabooButchery.butcherGuilt",
        "tabooButchery.butcherTabooMemoryCount", "tabooButchery.witnessMood",
        "tabooButchery.witnessFactCount", "tabooButchery.witnessRumorCount",
        "tabooButchery.exactOutcomeCount", "tabooButchery.physicalStackCursor"
    };

    private static JObject[] TabooButcheryInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(stage));
        bool afterDeath = stage == 1;
        return new[]
        {
            Predicate("initial", "tabooButchery.stage", "equal", stage),
            Predicate("initial", "tabooButchery.absoluteDay", "equal", 7),
            Predicate("initial", "tabooButchery.butcherId", "equal", "owner"),
            Predicate("initial", "tabooButchery.butcherName", "equal", "오크 사장"),
            Predicate("initial", "tabooButchery.butcherSpecies", "equal", "Orc"),
            Predicate("initial", "tabooButchery.victimName", "equal", "그롬"),
            Predicate("initial", "tabooButchery.victimSpecies", "equal", "Orc"),
            Predicate("initial", "tabooButchery.victimHealth", "equal", afterDeath ? 0d : 100d),
            Predicate("initial", "tabooButchery.victimDead", "equal", afterDeath),
            Predicate("initial", "tabooButchery.witnessName", "equal", "일반 직원"),
            Predicate("initial", "tabooButchery.witnessSpecies", "equal", "Slime"),
            Predicate("initial", "tabooButchery.corpseQuantity", "equal", stage),
            Predicate("initial", "tabooButchery.corpseSourceName", "equal", afterDeath ? "그롬" : string.Empty),
            Predicate("initial", "tabooButchery.corpseSourceSpecies", "equal", afterDeath ? "Orc" : string.Empty),
            Predicate("initial", "tabooButchery.corpseDeathReason", "equal", afterDeath ? "Starvation" : string.Empty),
            Predicate("initial", "tabooButchery.corpseAuthorized", "equal", afterDeath),
            Predicate("initial", "tabooButchery.bloodAmount", "equal", afterDeath ? 12d : 0d),
            Predicate("initial", "tabooButchery.meatQuantity", "equal", 0),
            Predicate("initial", "tabooButchery.boneQuantity", "equal", 0),
            Predicate("initial", "tabooButchery.butcherHygiene", "equal", 75d),
            Predicate("initial", "tabooButchery.butcherGuilt", "equal", 0d),
            Predicate("initial", "tabooButchery.butcherTabooMemoryCount", "equal", 0),
            Predicate("initial", "tabooButchery.witnessMood", "equal", 0d),
            Predicate("initial", "tabooButchery.witnessFactCount", "equal", 0),
            Predicate("initial", "tabooButchery.witnessRumorCount", "equal", 0),
            Predicate("initial", "tabooButchery.exactOutcomeCount", "equal", stage * 2),
            Predicate("initial", "tabooButchery.physicalStackCursor", "equal", stage)
        };
    }

    private static string[] OffenseArrivalStateKeys() => new[]
    {
        "offenseArrival.ownerId", "offenseArrival.stage", "offenseArrival.day",
        "offenseArrival.expeditionId", "offenseArrival.targetId",
        "offenseArrival.targetTitle", "offenseArrival.authoredRewardType",
        "offenseArrival.arrivalId", "offenseArrival.arrivalKind",
        "offenseArrival.arrivalStage", "offenseArrival.requestedAmount",
        "offenseArrival.returnSealed", "offenseArrival.materializedCount",
        "offenseArrival.escapedCount", "offenseArrival.liveWildlifeCount",
        "offenseArrival.materializedWildlifeId",
        "offenseArrival.materializedSpeciesId",
        "offenseArrival.materializedMaxHealth",
        "offenseArrival.materializedArrivalHealth",
        "offenseArrival.resultHistoryCount",
        "offenseArrival.resultReceiptResolution",
        "offenseArrival.resultReceiptMaterialized",
        "offenseArrival.resultReceiptSecured",
        "offenseArrival.resultReceiptEscaped",
        "offenseArrival.metaOffenseSuccessCount",
        "offenseArrival.campaignTargetCompleted",
        "offenseArrival.resultOutcomeCount",
        "offenseArrival.arrivalOutcomeCount",
        "offenseArrival.exactOutcomeCount"
    };

    private static JObject[] OffenseArrivalInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        bool pending = stage == 1;
        return new[]
        {
            Predicate("initial", "offenseArrival.stage", "equal", stage),
            Predicate("initial", "offenseArrival.day", "equal", 37),
            Predicate("initial", "offenseArrival.targetId", "equal", "rival_dungeon"),
            Predicate("initial", "offenseArrival.authoredRewardType", "equal",
                OffenseRewardTypeIds.SpecialMonster),
            Predicate("initial", "offenseArrival.arrivalId", "equal",
                pending ? "return:1" : "none"),
            Predicate("initial", "offenseArrival.arrivalKind", "equal",
                pending ? OffenseReturnArrivalKind.SpecialWildlife.ToString() : "None"),
            Predicate("initial", "offenseArrival.arrivalStage", "equal",
                pending ? OffenseReturnArrivalStage.WaitingForParty.ToString() : "None"),
            Predicate("initial", "offenseArrival.requestedAmount", "equal", stage),
            Predicate("initial", "offenseArrival.returnSealed", "equal", pending),
            Predicate("initial", "offenseArrival.materializedCount", "equal", 0),
            Predicate("initial", "offenseArrival.escapedCount", "equal", 0),
            Predicate("initial", "offenseArrival.liveWildlifeCount", "equal", 0),
            Predicate("initial", "offenseArrival.resultHistoryCount", "equal", stage),
            Predicate("initial", "offenseArrival.resultReceiptResolution", "equal",
                pending ? OffenseExpeditionArrivalResolution.Pending.ToString() : "None"),
            Predicate("initial", "offenseArrival.metaOffenseSuccessCount", "equal", stage),
            Predicate("initial", "offenseArrival.campaignTargetCompleted", "equal", pending),
            Predicate("initial", "offenseArrival.resultOutcomeCount", "equal", stage),
            Predicate("initial", "offenseArrival.arrivalOutcomeCount", "equal", 0),
            Predicate("initial", "offenseArrival.exactOutcomeCount", "equal", stage)
        };
    }

    private static string[] FestivalCelebrationStateKeys() => new[]
    {
        "festival.ownerId", "festival.stage", "festival.festivalId",
        "festival.displayName", "festival.currentYear",
        "festival.currentAbsoluteDay", "festival.participantCount",
        "festival.occurrenceCount", "festival.resolvedOccurrenceCount",
        "festival.successfulAttendanceCount", "festival.candleQuantity",
        "festival.activeInputOwnerCount",
        "festival.pendingMaterialReceiptCount",
        "festival.cleanupCompleteCount", "festival.moodFactorCount",
        "festival.attendanceRecordCount", "festival.griefRemainingMultiplier",
        "festival.factionRapportTotal", "festival.exactOutcomeCount",
        "festival.controlledMaterialPlacement",
        "festival.naturalDeliveryTelemetry"
    };

    private static JObject[] FestivalCelebrationInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        return new[]
        {
            Predicate("initial", "festival.stage", "equal", stage),
            Predicate("initial", "festival.festivalId", "equal",
                "festival:long-night-memorial"),
            Predicate("initial", "festival.participantCount", "equal", 10),
            Predicate("initial", "festival.occurrenceCount", "equal", stage),
            Predicate("initial", "festival.resolvedOccurrenceCount", "equal", stage),
            Predicate("initial", "festival.successfulAttendanceCount", "equal", stage * 10),
            Predicate("initial", "festival.candleQuantity", "equal", 0),
            Predicate("initial", "festival.activeInputOwnerCount", "equal", 0),
            Predicate("initial", "festival.pendingMaterialReceiptCount", "equal", 0),
            Predicate("initial", "festival.cleanupCompleteCount", "equal", stage),
            Predicate("initial", "festival.moodFactorCount", "equal", stage * 10),
            Predicate("initial", "festival.attendanceRecordCount", "equal", stage * 10),
            Predicate("initial", "festival.griefRemainingMultiplier", "equal",
                stage == 0 ? 1d : 0.75d),
            Predicate("initial", "festival.exactOutcomeCount", "equal", stage),
            Predicate("initial", "festival.controlledMaterialPlacement", "equal", true),
            Predicate("initial", "festival.naturalDeliveryTelemetry", "equal", false)
        };
    }

    private static string[] RunVariableStateKeys() => new[]
    {
        "runVariable.ownerId", "runVariable.stage", "runVariable.runSeed",
        "runVariable.currentDay", "runVariable.activeCount",
        "runVariable.activeVariableId", "runVariable.remainingDays",
        "runVariable.instanceSequence", "runVariable.nextOperationSequence",
        "runVariable.lastOperationAdvanceDay",
        "runVariable.activationOutcomeCount",
        "runVariable.expirationOutcomeCount",
        "runVariable.exactOutcomeCount"
    };

    private static JObject[] RunVariableInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        return new[]
        {
            Predicate("initial", "runVariable.stage", "equal", stage),
            Predicate("initial", "runVariable.runSeed", "equal", 54123),
            Predicate("initial", "runVariable.currentDay", "equal", 1),
            Predicate("initial", "runVariable.activeCount", "equal", stage),
            Predicate("initial", "runVariable.activeVariableId", "equal",
                stage == 0 ? "none" : "run:causal-operation-variable"),
            Predicate("initial", "runVariable.remainingDays", "equal", stage),
            Predicate("initial", "runVariable.instanceSequence", "equal", stage),
            Predicate("initial", "runVariable.nextOperationSequence", "equal",
                stage == 0 ? 1 : 2),
            Predicate("initial", "runVariable.lastOperationAdvanceDay", "equal", 0),
            Predicate("initial", "runVariable.activationOutcomeCount", "equal", stage),
            Predicate("initial", "runVariable.expirationOutcomeCount", "equal", 0),
            Predicate("initial", "runVariable.exactOutcomeCount", "equal", stage)
        };
    }

    private static string[] MetaProgressionStateKeys() => new[]
    {
        "metaProgression.ownerId", "metaProgression.stage",
        "metaProgression.lifetimeEarnedCurrency",
        "metaProgression.spentCurrency", "metaProgression.availableCurrency",
        "metaProgression.completedRunCount", "metaProgression.ended",
        "metaProgression.hasLatestResult",
        "metaProgression.latestOwnerName",
        "metaProgression.latestEndReason",
        "metaProgression.latestOutcome",
        "metaProgression.latestLegacyCurrency",
        "metaProgression.upgradeId", "metaProgression.upgradeLevel",
        "metaProgression.runResultOutcomeCount",
        "metaProgression.upgradeOutcomeCount",
        "metaProgression.exactOutcomeCount"
    };

    private static JObject[] MetaProgressionInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        bool completed = stage == 1;
        return new[]
        {
            Predicate("initial", "metaProgression.stage", "equal", stage),
            Predicate("initial", "metaProgression.lifetimeEarnedCurrency", "equal",
                completed ? 35 : 0),
            Predicate("initial", "metaProgression.spentCurrency", "equal", 0),
            Predicate("initial", "metaProgression.availableCurrency", "equal",
                completed ? 35 : 0),
            Predicate("initial", "metaProgression.completedRunCount", "equal", stage),
            Predicate("initial", "metaProgression.ended", "equal", completed),
            Predicate("initial", "metaProgression.hasLatestResult", "equal", completed),
            Predicate("initial", "metaProgression.latestOwnerName", "equal",
                completed ? "연화" : "none"),
            Predicate("initial", "metaProgression.latestEndReason", "equal",
                completed ? "던전 계승 준비" : "none"),
            Predicate("initial", "metaProgression.latestOutcome", "equal",
                completed ? "Victory" : "None"),
            Predicate("initial", "metaProgression.latestLegacyCurrency", "equal",
                completed ? 35 : 0),
            Predicate("initial", "metaProgression.upgradeId", "equal",
                "meta:causal-legacy-ledger"),
            Predicate("initial", "metaProgression.upgradeLevel", "equal", 0),
            Predicate("initial", "metaProgression.runResultOutcomeCount", "equal",
                stage),
            Predicate("initial", "metaProgression.upgradeOutcomeCount", "equal", 0),
            Predicate("initial", "metaProgression.exactOutcomeCount", "equal", stage)
        };
    }

    private static string[] CombatLifeCycleStateKeys() => new[]
    {
        "health.ownerId", "health.actorId", "health.attackerId",
        "health.ownerSnapshot", "health.stage", "health.currentHealth",
        "health.downed", "health.attackOutcomeCount",
        "health.thresholdOutcomeCount", "health.downedOutcomeCount",
        "health.recoveredOutcomeCount", "health.deathOutcomeCount",
        "health.killedOutcomeCount", "health.exactOutcomeCount"
    };

    private static JObject[] CombatLifeCycleInitialPredicates(int stage)
    {
        if (stage is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(stage));
        int attackCount = stage >= 1 ? 1 : 0;
        int thresholdCount = stage switch
        {
            0 => 0,
            1 => 1,
            _ => 2
        };
        return new[]
        {
            Predicate("initial", "health.stage", "equal", stage),
            Predicate("initial", "health.currentHealth", "equal", stage switch
            {
                0 => 25f,
                1 => 1f,
                _ => 41f
            }),
            Predicate("initial", "health.downed", "equal", stage == 1),
            Predicate("initial", "health.attackOutcomeCount", "equal", attackCount),
            Predicate("initial", "health.thresholdOutcomeCount", "equal",
                thresholdCount),
            Predicate("initial", "health.downedOutcomeCount", "equal",
                stage >= 1 ? 1 : 0),
            Predicate("initial", "health.recoveredOutcomeCount", "equal",
                stage >= 2 ? 1 : 0),
            Predicate("initial", "health.deathOutcomeCount", "equal", 0),
            Predicate("initial", "health.killedOutcomeCount", "equal", 0),
            Predicate("initial", "health.exactOutcomeCount", "equal", stage switch
            {
                0 => 0,
                1 => 4,
                _ => 6
            })
        };
    }

    private static string[] StockSupplyTransformStateKeys() => new[]
    {
        "stockTransform.ownerId", "stockTransform.stage",
        "stockTransform.absoluteDay", "stockTransform.money",
        "stockTransform.sourceItemId", "stockTransform.sourceStackId",
        "stockTransform.sourceQuantity", "stockTransform.sourceX",
        "stockTransform.sourceY", "stockTransform.outputItemId",
        "stockTransform.outputQuantity", "stockTransform.stackCount",
        "stockTransform.supplyOutcomeCount",
        "stockTransform.transformOutcomeCount",
        "stockTransform.migratedOwnerCount",
        "stockTransform.exactOutcomeCount"
    };

    private static JObject[] StockSupplyTransformInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        bool supplied = stage == 1;
        return new[]
        {
            Predicate("initial", "stockTransform.stage", "equal", stage),
            Predicate("initial", "stockTransform.absoluteDay", "equal", 3),
            Predicate("initial", "stockTransform.money", "equal",
                supplied ? 88 : 100),
            Predicate("initial", "stockTransform.sourceItemId", "equal",
                "material:lumber"),
            Predicate("initial", "stockTransform.sourceQuantity", "equal",
                supplied ? 3 : 0),
            Predicate("initial", "stockTransform.outputQuantity", "equal", 0),
            Predicate("initial", "stockTransform.stackCount", "equal",
                supplied ? 1 : 0),
            Predicate("initial", "stockTransform.supplyOutcomeCount", "equal",
                stage),
            Predicate("initial", "stockTransform.transformOutcomeCount", "equal",
                0),
            Predicate("initial", "stockTransform.migratedOwnerCount", "equal",
                stage),
            Predicate("initial", "stockTransform.exactOutcomeCount", "equal",
                stage)
        };
    }

    private static string[] FacilityWishlistProcurementStateKeys() => new[]
    {
        "facilityProcurement.ownerId", "facilityProcurement.stage",
        "facilityProcurement.absoluteDay", "facilityProcurement.money",
        "facilityProcurement.lastProcessedDay",
        "facilityProcurement.dailyBudget",
        "facilityProcurement.minimumReserve",
        "facilityProcurement.wishlistRuleCount",
        "facilityProcurement.resultCount",
        "facilityProcurement.purchasedResultCount",
        "facilityProcurement.processedOfferKeyCount",
        "facilityProcurement.buildingId",
        "facilityProcurement.facilityCost",
        "facilityProcurement.buildingUnlockedCount",
        "facilityProcurement.installationKitCount",
        "facilityProcurement.autoOutcomeCount",
        "facilityProcurement.facilityOutcomeCount",
        "facilityProcurement.migratedOwnerCount",
        "facilityProcurement.exactOutcomeCount"
    };

    private static JObject[] FacilityWishlistProcurementInitialPredicates(
        int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        return new[]
        {
            Predicate("initial", "facilityProcurement.stage", "equal", stage),
            Predicate("initial", "facilityProcurement.absoluteDay", "equal", 5),
            Predicate("initial", "facilityProcurement.money", "equal", 1000),
            Predicate("initial", "facilityProcurement.lastProcessedDay", "equal",
                stage == 0 ? 0 : 5),
            Predicate("initial", "facilityProcurement.dailyBudget", "equal",
                stage == 0 ? 500 : 0),
            Predicate("initial", "facilityProcurement.minimumReserve", "equal", 0),
            Predicate("initial", "facilityProcurement.wishlistRuleCount", "equal", 0),
            Predicate("initial", "facilityProcurement.resultCount", "equal", stage),
            Predicate("initial", "facilityProcurement.purchasedResultCount", "equal", 0),
            Predicate("initial", "facilityProcurement.processedOfferKeyCount", "equal", 0),
            Predicate("initial", "facilityProcurement.facilityCost", "equal", 0),
            Predicate("initial", "facilityProcurement.buildingUnlockedCount", "equal", 0),
            Predicate("initial", "facilityProcurement.installationKitCount", "equal", 0),
            Predicate("initial", "facilityProcurement.autoOutcomeCount", "equal", stage),
            Predicate("initial", "facilityProcurement.facilityOutcomeCount", "equal", 0),
            Predicate("initial", "facilityProcurement.migratedOwnerCount", "equal", stage),
            Predicate("initial", "facilityProcurement.exactOutcomeCount", "equal", stage)
        };
    }

    private static string[] ObservedCareerArcStateKeys() => new[]
    {
        "careerArc.ownerId", "careerArc.stage", "careerArc.absoluteDay",
        "careerArc.mentorId", "careerArc.studentId", "careerArc.academyId",
        "careerArc.proficiencyId", "careerArc.successfulReceiptCount",
        "careerArc.activeEventCount", "careerArc.resolvedEventCount",
        "careerArc.mentorshipAwardOutcomeCount",
        "careerArc.lastLessonOutcomeCount",
        "careerArc.quietPromotionOutcomeCount",
        "careerArc.funeralOutcomeCount",
        "careerArc.lineageCompressionOutcomeCount",
        "careerArc.mentorTombstoneCount",
        "careerArc.lineageSummaryCount", "careerArc.lineageWorldFlagCount",
        "careerArc.migratedOwnerCount",
        "careerArc.exactOutcomeCount"
    };

    private static JObject[] ObservedCareerArcInitialPredicates(int stage)
    {
        if (stage is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(stage));
        return new[]
        {
            Predicate("initial", "careerArc.stage", "equal", stage),
            Predicate("initial", "careerArc.absoluteDay", "equal",
                stage switch
                {
                    0 => 41,
                    1 => 42,
                    2 => 42,
                    3 => 43,
                    _ => 44
                }),
            Predicate("initial", "careerArc.mentorId", "equal",
                "character:causal-career-arc:mentor"),
            Predicate("initial", "careerArc.studentId", "equal",
                "character:causal-career-arc:student"),
            Predicate("initial", "careerArc.academyId", "equal",
                "building:causal-career-arc:academy"),
            Predicate("initial", "careerArc.proficiencyId", "equal",
                "proficiency:crafting"),
            Predicate("initial", "careerArc.successfulReceiptCount", "equal",
                Math.Max(0, stage - 1)),
            Predicate("initial", "careerArc.activeEventCount", "equal",
                stage < 2 ? 0 : 1),
            Predicate("initial", "careerArc.resolvedEventCount", "equal",
                Math.Max(0, stage - 2)),
            Predicate("initial", "careerArc.mentorshipAwardOutcomeCount",
                "equal", stage >= 1 ? 1 : 0),
            Predicate("initial", "careerArc.lastLessonOutcomeCount", "equal",
                stage >= 2 ? 1 : 0),
            Predicate("initial", "careerArc.quietPromotionOutcomeCount", "equal",
                stage >= 3 ? 1 : 0),
            Predicate("initial", "careerArc.funeralOutcomeCount", "equal",
                stage >= 4 ? 1 : 0),
            Predicate("initial", "careerArc.lineageCompressionOutcomeCount",
                "equal", 0),
            Predicate("initial", "careerArc.mentorTombstoneCount", "equal",
                stage == 4 ? 1 : 0),
            Predicate("initial", "careerArc.lineageSummaryCount", "equal", 0),
            Predicate("initial", "careerArc.lineageWorldFlagCount", "equal", 0),
            Predicate("initial", "careerArc.migratedOwnerCount", "equal", stage),
            Predicate("initial", "careerArc.exactOutcomeCount", "equal", stage)
        };
    }

    private static string[] ObservedMealIncidentResolutionStateKeys() => new[]
    {
        "mealIncident.ownerId", "mealIncident.stage",
        "mealIncident.absoluteDay", "mealIncident.customerId",
        "mealIncident.itemDefinitionId", "mealIncident.itemStackId",
        "mealIncident.definitionId", "mealIncident.occurrenceId",
        "mealIncident.resolutionId", "mealIncident.observedCause",
        "mealIncident.successfulReceiptCount",
        "mealIncident.activeEventCount", "mealIncident.resolvedEventCount",
        "mealIncident.workDelayDays",
        "mealIncident.observationOutcomeCount",
        "mealIncident.resolutionOutcomeCount",
        "mealIncident.migratedOwnerCount", "mealIncident.exactOutcomeCount"
    };

    private static JObject[] ObservedMealIncidentResolutionInitialPredicates(
        int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        return new[]
        {
            Predicate("initial", "mealIncident.stage", "equal", stage),
            Predicate("initial", "mealIncident.absoluteDay", "equal",
                stage == 0 ? 41 : 42),
            Predicate("initial", "mealIncident.customerId", "equal",
                "character:causal-observed-meal:customer"),
            Predicate("initial", "mealIncident.itemDefinitionId", "equal",
                "food:lavish-meat"),
            Predicate("initial", "mealIncident.itemStackId", "equal",
                "stack:causal-observed-meal:contaminated"),
            Predicate("initial", "mealIncident.definitionId", "equal",
                "service-incident:contamination"),
            Predicate("initial", "mealIncident.successfulReceiptCount", "equal",
                stage),
            Predicate("initial", "mealIncident.activeEventCount", "equal",
                stage),
            Predicate("initial", "mealIncident.resolvedEventCount", "equal", 0),
            Predicate("initial", "mealIncident.workDelayDays", "equal", 0),
            Predicate("initial", "mealIncident.observationOutcomeCount", "equal",
                stage),
            Predicate("initial", "mealIncident.resolutionOutcomeCount", "equal",
                0),
            Predicate("initial", "mealIncident.migratedOwnerCount", "equal",
                stage),
            Predicate("initial", "mealIncident.exactOutcomeCount", "equal", stage)
        };
    }

    private static string[] RegularCustomerStateKeys() => new[]
    {
        "regularCustomer.ownerId", "regularCustomer.stage",
        "regularCustomer.absoluteDay", "regularCustomer.customerId",
        "regularCustomer.displayName", "regularCustomer.visitCount",
        "regularCustomer.averageSatisfaction", "regularCustomer.status",
        "regularCustomer.isRegular", "regularCustomer.isRecruitCandidate",
        "regularCustomer.isRecruited",
        "regularCustomer.recruitedAbsoluteDay",
        "regularCustomer.actorType", "regularCustomer.deliveryPending",
        "regularCustomer.visitOutcomeCount",
        "regularCustomer.recruitOutcomeCount",
        "regularCustomer.exactOutcomeCount"
    };

    private static JObject[] RegularCustomerInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        bool visited = stage == 1;
        return new[]
        {
            Predicate("initial", "regularCustomer.stage", "equal", stage),
            Predicate("initial", "regularCustomer.absoluteDay", "equal", 20),
            Predicate("initial", "regularCustomer.customerId", "equal",
                "character:world:causal:regular-customer"),
            Predicate("initial", "regularCustomer.displayName", "equal", "연화"),
            Predicate("initial", "regularCustomer.visitCount", "equal", stage),
            Predicate("initial", "regularCustomer.averageSatisfaction", "equal",
                visited ? 90f : 0f),
            Predicate("initial", "regularCustomer.status", "equal", "Visitor"),
            Predicate("initial", "regularCustomer.isRegular", "equal", false),
            Predicate("initial", "regularCustomer.isRecruitCandidate", "equal", false),
            Predicate("initial", "regularCustomer.isRecruited", "equal", false),
            Predicate("initial", "regularCustomer.recruitedAbsoluteDay", "equal", 0),
            Predicate("initial", "regularCustomer.actorType", "equal", "Customer"),
            Predicate("initial", "regularCustomer.deliveryPending", "equal", false),
            Predicate("initial", "regularCustomer.visitOutcomeCount", "equal", stage),
            Predicate("initial", "regularCustomer.recruitOutcomeCount", "equal", 0),
            Predicate("initial", "regularCustomer.exactOutcomeCount", "equal", stage)
        };
    }

    private static string[] CharacterLifeStateKeys() => new[]
    {
        "characterLife.ownerId", "characterLife.stage",
        "characterLife.absoluteDay", "characterLife.characterId",
        "characterLife.chronologicalAgeDays",
        "characterLife.biologicalAgeDayUnits", "characterLife.lifeStage",
        "characterLife.conditionId", "characterLife.conditionSeverity",
        "characterLife.conditionNextProgressDayUnits",
        "characterLife.ageConditionOutcomeCount",
        "characterLife.lifeStageOutcomeCount",
        "characterLife.exactOutcomeCount"
    };

    private static JObject[] CharacterLifeInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        bool progressed = stage == 1;
        return new[]
        {
            Predicate("initial", "characterLife.stage", "equal", stage),
            Predicate("initial", "characterLife.absoluteDay", "equal",
                progressed ? 2 : 1),
            Predicate("initial", "characterLife.characterId", "equal",
                "character:world:causal:life-progression"),
            Predicate("initial", "characterLife.chronologicalAgeDays", "equal",
                progressed ? 101 : 100),
            Predicate("initial", "characterLife.biologicalAgeDayUnits", "equal",
                progressed ? 1196d : 1192d),
            Predicate("initial", "characterLife.lifeStage", "equal", "Adolescent"),
            Predicate("initial", "characterLife.conditionId", "equal",
                "age:causal:life-progression"),
            Predicate("initial", "characterLife.conditionSeverity", "equal", stage),
            Predicate("initial", "characterLife.conditionNextProgressDayUnits",
                "equal", progressed ? 1316d : 1196d),
            Predicate("initial", "characterLife.ageConditionOutcomeCount", "equal",
                stage),
            Predicate("initial", "characterLife.lifeStageOutcomeCount", "equal", 0),
            Predicate("initial", "characterLife.exactOutcomeCount", "equal", stage)
        };
    }

    private static string[] FactionRouteStateKeys() => new[]
    {
        "factionRoute.ownerId", "factionRoute.stage",
        "factionRoute.absoluteDay", "factionRoute.factionId",
        "factionRoute.routeId", "factionRoute.routeCount",
        "factionRoute.routeStatus", "factionRoute.pathIndex",
        "factionRoute.segmentProgress", "factionRoute.settlementState",
        "factionRoute.settlementPaymentGold",
        "factionRoute.settlementBalanceBefore",
        "factionRoute.settlementBalanceAfter",
        "factionRoute.cargoDeliveryState",
        "factionRoute.cargoStackCount",
        "factionRoute.cargoDeliveredQuantity",
        "factionRoute.sessionMoney",
        "factionRoute.settlementOutcomeCount",
        "factionRoute.arrivalOutcomeCount",
        "factionRoute.cargoOutcomeCount",
        "factionRoute.exactOutcomeCount"
    };

    private static JObject[] FactionRouteInitialPredicates(int stage)
    {
        if (stage is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(stage));
        bool hasRoute = stage > 0;
        bool arrived = stage == 2;
        return new[]
        {
            Predicate("initial", "factionRoute.stage", "equal", stage),
            Predicate("initial", "factionRoute.absoluteDay", "equal", 6),
            Predicate("initial", "factionRoute.factionId", "equal",
                "faction:causal:trade-route"),
            Predicate("initial", "factionRoute.routeId", "equal",
                hasRoute ? "faction-route:1" : string.Empty),
            Predicate("initial", "factionRoute.routeCount", "equal",
                hasRoute ? 1 : 0),
            Predicate("initial", "factionRoute.routeStatus", "equal",
                arrived ? "Arrived" : hasRoute ? "Traveling" : "None"),
            Predicate("initial", "factionRoute.pathIndex", "equal",
                arrived ? 1 : 0),
            Predicate("initial", "factionRoute.segmentProgress", "equal", 0f),
            Predicate("initial", "factionRoute.settlementState", "equal",
                hasRoute ? "Paid" : "None"),
            Predicate("initial", "factionRoute.settlementPaymentGold", "equal",
                hasRoute ? 10 : 0),
            Predicate("initial", "factionRoute.settlementBalanceBefore", "equal",
                1000),
            Predicate("initial", "factionRoute.settlementBalanceAfter", "equal",
                hasRoute ? 990 : 1000),
            Predicate("initial", "factionRoute.cargoDeliveryState", "equal",
                hasRoute ? "Ready" : "None"),
            Predicate("initial", "factionRoute.cargoStackCount", "equal", 0),
            Predicate("initial", "factionRoute.cargoDeliveredQuantity", "equal", 0),
            Predicate("initial", "factionRoute.sessionMoney", "equal",
                hasRoute ? 990 : 1000),
            Predicate("initial", "factionRoute.settlementOutcomeCount", "equal",
                hasRoute ? 1 : 0),
            Predicate("initial", "factionRoute.arrivalOutcomeCount", "equal",
                arrived ? 1 : 0),
            Predicate("initial", "factionRoute.cargoOutcomeCount", "equal", 0),
            Predicate("initial", "factionRoute.exactOutcomeCount", "equal", stage)
        };
    }

    private static string[] ConsumablesStateKeys() => new[]
    {
        "consumables.ownerId", "consumables.stage",
        "consumables.absoluteDay", "consumables.characterId",
        "consumables.completedOperationCount",
        "consumables.substanceStateCount",
        "consumables.substanceTolerance",
        "consumables.hungerRecovered", "consumables.moodApplied",
        "consumables.damageApplied",
        "consumables.acknowledgementCount",
        "consumables.pendingCommitCount",
        "consumables.mealOutcomeCount",
        "consumables.substanceOutcomeCount",
        "consumables.exactOutcomeCount"
    };

    private static JObject[] ConsumablesInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        bool ateMeal = stage == 1;
        return new[]
        {
            Predicate("initial", "consumables.stage", "equal", stage),
            Predicate("initial", "consumables.absoluteDay", "equal", 42),
            Predicate("initial", "consumables.characterId", "equal",
                "character:causal:consumables-progression"),
            Predicate("initial", "consumables.completedOperationCount", "equal",
                stage),
            Predicate("initial", "consumables.substanceStateCount", "equal", 0),
            Predicate("initial", "consumables.substanceTolerance", "equal", 0f),
            Predicate("initial", "consumables.hungerRecovered", "equal",
                ateMeal ? 30f : 0f),
            Predicate("initial", "consumables.moodApplied", "equal",
                ateMeal ? 3f : 0f),
            Predicate("initial", "consumables.damageApplied", "equal", 0f),
            Predicate("initial", "consumables.acknowledgementCount", "equal",
                stage),
            Predicate("initial", "consumables.pendingCommitCount", "equal", 0),
            Predicate("initial", "consumables.mealOutcomeCount", "equal", stage),
            Predicate("initial", "consumables.substanceOutcomeCount", "equal", 0),
            Predicate("initial", "consumables.exactOutcomeCount", "equal", stage)
        };
    }

    private static string[] MealShortageStateKeys() => new[]
    {
        "mealShortage.ownerId", "mealShortage.stage",
        "mealShortage.characterId", "mealShortage.lastProcessedDay",
        "mealShortage.neededFood", "mealShortage.consumedFood",
        "mealShortage.missingFood", "mealShortage.neededWater",
        "mealShortage.consumedWater", "mealShortage.missingWater",
        "mealShortage.foodShortageDays", "mealShortage.waterShortageDays",
        "mealShortage.sanitationRisk", "mealShortage.diseaseRisk",
        "mealShortage.exteriorNightDanger", "mealShortage.mealLedgerCount",
        "mealShortage.healthStateCount", "mealShortage.outcomeCount",
        "mealShortage.exactOutcomeCount"
    };

    private static JObject[] MealShortageInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        return new[]
        {
            Predicate("initial", "mealShortage.stage", "equal", stage),
            Predicate("initial", "mealShortage.characterId", "equal",
                "character:causal:meal-shortage"),
            Predicate("initial", "mealShortage.lastProcessedDay", "equal",
                stage == 0 ? 0 : 2),
            Predicate("initial", "mealShortage.neededFood", "equal", stage),
            Predicate("initial", "mealShortage.consumedFood", "equal", 0),
            Predicate("initial", "mealShortage.missingFood", "equal", stage),
            Predicate("initial", "mealShortage.neededWater", "equal", stage),
            Predicate("initial", "mealShortage.consumedWater", "equal", 0),
            Predicate("initial", "mealShortage.missingWater", "equal", stage),
            Predicate("initial", "mealShortage.mealLedgerCount", "equal", 0),
            Predicate("initial", "mealShortage.healthStateCount", "equal", 0),
            Predicate("initial", "mealShortage.outcomeCount", "equal", stage),
            Predicate("initial", "mealShortage.exactOutcomeCount", "equal", stage)
        };
    }

    private static string[] PrimitiveSurvivalStateKeys() => new[]
    {
        "primitiveSurvival.ownerId", "primitiveSurvival.stage",
        "primitiveSurvival.characterId", "primitiveSurvival.calendarDay",
        "primitiveSurvival.sleep", "primitiveSurvival.hygiene",
        "primitiveSurvival.excretion", "primitiveSurvival.baseMood",
        "primitiveSurvival.moodFactorCount",
        "primitiveSurvival.floorRestMood",
        "primitiveSurvival.outcomeOwnerRevision",
        "primitiveSurvival.outcomeCount",
        "primitiveSurvival.exactOutcomeCount"
    };

    private static JObject[] PrimitiveSurvivalInitialPredicates(int stage)
    {
        if (stage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(stage));
        return new[]
        {
            Predicate("initial", "primitiveSurvival.stage", "equal", stage),
            Predicate("initial", "primitiveSurvival.characterId", "equal",
                "character:causal:primitive-survival"),
            Predicate("initial", "primitiveSurvival.calendarDay", "equal",
                stage == 0 ? 13 : 14),
            Predicate("initial", "primitiveSurvival.sleep", "equal",
                stage == 0 ? 0f : 55f),
            Predicate("initial", "primitiveSurvival.hygiene", "equal",
                stage == 0 ? 60f : 56f),
            Predicate("initial", "primitiveSurvival.excretion", "equal", 100f),
            Predicate("initial", "primitiveSurvival.moodFactorCount", "equal",
                stage),
            Predicate("initial", "primitiveSurvival.floorRestMood", "equal",
                stage == 0 ? 0f : -3f),
            Predicate("initial", "primitiveSurvival.outcomeOwnerRevision",
                "equal", stage),
            Predicate("initial", "primitiveSurvival.outcomeCount", "equal",
                stage),
            Predicate("initial", "primitiveSurvival.exactOutcomeCount", "equal",
                stage)
        };
    }

    private static string[] ServiceHubDayStateKeys() => new[]
    {
        "serviceHubDay.ownerId", "serviceHubDay.stage",
        "serviceHubDay.facilityId", "serviceHubDay.visitorId",
        "serviceHubDay.calendarDay", "serviceHubDay.completedUses",
        "serviceHubDay.serviceMode", "serviceHubDay.settlementDay",
        "serviceHubDay.settlementVisits", "serviceHubDay.reportCount",
        "serviceHubDay.latestReportDay",
        "serviceHubDay.latestReportVisits", "serviceHubDay.money",
        "serviceHubDay.visitOwnerRevision",
        "serviceHubDay.modeOwnerRevision",
        "serviceHubDay.dayOwnerRevision",
        "serviceHubDay.exactOutcomeCount"
    };

    private static JObject[] ServiceHubDayInitialPredicates(int stage)
    {
        if (stage is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(stage));
        return new[]
        {
            Predicate("initial", "serviceHubDay.stage", "equal", stage),
            Predicate("initial", "serviceHubDay.facilityId", "equal",
                "building:causal:service-hub"),
            Predicate("initial", "serviceHubDay.visitorId", "equal",
                "character:causal:service-hub-visitor"),
            Predicate("initial", "serviceHubDay.calendarDay", "equal", 17),
            Predicate("initial", "serviceHubDay.completedUses", "equal",
                stage == 0 ? 0 : 1),
            Predicate("initial", "serviceHubDay.serviceMode", "equal",
                stage < 2 ? "Managed" : "Direct"),
            Predicate("initial", "serviceHubDay.settlementDay", "equal", 17),
            Predicate("initial", "serviceHubDay.settlementVisits", "equal",
                stage == 0 ? 0 : 1),
            Predicate("initial", "serviceHubDay.reportCount", "equal", 0),
            Predicate("initial", "serviceHubDay.latestReportDay", "equal", 0),
            Predicate("initial", "serviceHubDay.latestReportVisits", "equal", 0),
            Predicate("initial", "serviceHubDay.money", "equal", 1000),
            Predicate("initial", "serviceHubDay.visitOwnerRevision", "equal",
                stage == 0 ? 0 : 1),
            Predicate("initial", "serviceHubDay.modeOwnerRevision", "equal",
                stage < 2 ? 0 : 1),
            Predicate("initial", "serviceHubDay.dayOwnerRevision", "equal", 0),
            Predicate("initial", "serviceHubDay.exactOutcomeCount", "equal",
                stage)
        };
    }

    private static string[] InvasionLifecycleStateKeys() => new[]
    {
        "invasionLifecycle.ownerId", "invasionLifecycle.stage",
        "invasionLifecycle.runtimeId",
        "invasionLifecycle.ownerCharacterId",
        "invasionLifecycle.facilityId", "invasionLifecycle.calendarDay",
        "invasionLifecycle.activeIntruders",
        "invasionLifecycle.intruderState",
        "invasionLifecycle.intruderHealth",
        "invasionLifecycle.hasBreachedDungeon",
        "invasionLifecycle.facilityDamageCount",
        "invasionLifecycle.facilityDamaged",
        "invasionLifecycle.frontCollapsed",
        "invasionLifecycle.startOwnerRevision",
        "invasionLifecycle.breachOwnerRevision",
        "invasionLifecycle.damageOwnerRevision",
        "invasionLifecycle.frontCollapseOwnerRevision",
        "invasionLifecycle.resolveOwnerRevision",
        "invasionLifecycle.resolutionStatus",
        "invasionLifecycle.exactOutcomeCount"
    };

    private static JObject[] InvasionLifecycleInitialPredicates(int stage)
    {
        if (stage is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(stage));
        var predicates = new List<JObject>
        {
            Predicate("initial", "invasionLifecycle.stage", "equal", stage),
            Predicate("initial", "invasionLifecycle.ownerCharacterId", "equal",
                "character:migrated-defense-target:causal-invasion-owner"),
            Predicate("initial", "invasionLifecycle.facilityId", "equal",
                "building:causal:invasion-damage-target"),
            Predicate("initial", "invasionLifecycle.calendarDay", "equal", 21),
            Predicate("initial", "invasionLifecycle.activeIntruders", "equal",
                stage == 0 ? 0 : 1),
            Predicate("initial", "invasionLifecycle.hasBreachedDungeon", "equal",
                stage >= 2),
            Predicate("initial", "invasionLifecycle.facilityDamageCount", "equal",
                stage >= 3 ? 1 : 0),
            Predicate("initial", "invasionLifecycle.facilityDamaged", "equal",
                stage >= 3),
            Predicate("initial", "invasionLifecycle.frontCollapsed", "equal",
                stage >= 4),
            Predicate("initial", "invasionLifecycle.startOwnerRevision", "equal",
                stage >= 1 ? 1 : 0),
            Predicate("initial", "invasionLifecycle.breachOwnerRevision", "equal",
                stage >= 2 ? 1 : 0),
            Predicate("initial", "invasionLifecycle.damageOwnerRevision", "equal",
                stage >= 3 ? 1 : 0),
            Predicate("initial", "invasionLifecycle.frontCollapseOwnerRevision", "equal",
                stage >= 4 ? 1 : 0),
            Predicate("initial", "invasionLifecycle.resolveOwnerRevision", "equal", 0),
            Predicate("initial", "invasionLifecycle.resolutionStatus", "equal", "Pending"),
            Predicate("initial", "invasionLifecycle.exactOutcomeCount", "equal", stage)
        };
        if (stage == 0)
        {
            predicates.Add(Predicate(
                "initial", "invasionLifecycle.runtimeId", "equal", string.Empty));
            predicates.Add(Predicate(
                "initial", "invasionLifecycle.intruderState", "equal", "Absent"));
            predicates.Add(Predicate(
                "initial", "invasionLifecycle.intruderHealth", "equal", 0f));
        }
        return predicates.ToArray();
    }

    private static JObject Action(
        string actionId,
        string domainId,
        string productionEntry,
        MigratedProducerOutcomeKind outcomeKind,
        IEnumerable<string> observedStateKeys,
        IEnumerable<string> subjectRoleIds,
        IEnumerable<string> narrativeFunctionIds,
        IEnumerable<MigratedProducerOutcomeKind> companionOutcomeKinds = null,
        IEnumerable<string> companionOutcomeRelations = null)
    {
        MigratedProducerOutcomeDefinition definition =
            MigratedProducerOutcomeCatalog.Get(outcomeKind);
        JObject[] companionOutcomes = (companionOutcomeKinds
                ?? Array.Empty<MigratedProducerOutcomeKind>())
            .Select(kind =>
            {
                MigratedProducerOutcomeDefinition companion =
                    MigratedProducerOutcomeCatalog.Get(kind);
                return new JObject
                {
                    ["outcomeKind"] = kind.ToString(),
                    ["outcomeTypeId"] = companion.OutcomeTypeId.Value
                };
            })
            .ToArray();
        string[] relations = (companionOutcomeRelations
            ?? Enumerable.Repeat("atomic-sibling", companionOutcomes.Length)).ToArray();
        Require(relations.Length == companionOutcomes.Length
            && relations.All(value => value is "atomic-sibling" or "post-commit-observer" or "post-commit-continuation"),
            "Every catalog companion requires one supported relation.");
        for (int index = 0; index < companionOutcomes.Length; index++)
            companionOutcomes[index]["relation"] = relations[index];
        return new JObject
        {
            ["actionId"] = actionId,
            ["domainId"] = domainId,
            ["productionEntry"] = productionEntry,
            ["argumentSchema"] = new JObject(),
            ["outcomeKind"] = outcomeKind.ToString(),
            ["outcomeTypeId"] = definition.OutcomeTypeId.Value,
            ["companionOutcomes"] = new JArray(companionOutcomes),
            ["observedStateKeys"] = new JArray(observedStateKeys),
            ["subjectRoleIds"] = new JArray(subjectRoleIds),
            ["narrativeFunctionIds"] = new JArray(narrativeFunctionIds),
            ["maxUsesPerPlan"] = 1,
            ["hypotheticalEffectsAllowed"] = false
        };
    }

    private static JObject Action(
        string actionId,
        string domainId,
        string productionEntry,
        string outcomeKind,
        GameplayOutcomeTypeId outcomeTypeId,
        IEnumerable<string> observedStateKeys,
        IEnumerable<string> subjectRoleIds,
        IEnumerable<string> narrativeFunctionIds,
        IEnumerable<JObject> companionOutcomes = null)
    {
        if (!outcomeTypeId.IsValid)
            throw new ArgumentException(
                "A valid gameplay outcome type is required.",
                nameof(outcomeTypeId));
        return new JObject
        {
            ["actionId"] = actionId,
            ["domainId"] = domainId,
            ["productionEntry"] = productionEntry,
            ["argumentSchema"] = new JObject(),
            ["outcomeKind"] = CausalGameplaySyntax.RequireId(
                outcomeKind,
                nameof(outcomeKind)),
            ["outcomeTypeId"] = outcomeTypeId.Value,
            ["companionOutcomes"] = new JArray(
                (companionOutcomes ?? Array.Empty<JObject>()).Select(value =>
                {
                    var declared = (JObject)value.DeepClone();
                    declared["relation"] ??= new JValue("atomic-sibling");
                    return declared;
                })),
            ["observedStateKeys"] = new JArray(observedStateKeys),
            ["subjectRoleIds"] = new JArray(subjectRoleIds),
            ["narrativeFunctionIds"] = new JArray(narrativeFunctionIds),
            ["maxUsesPerPlan"] = 1,
            ["hypotheticalEffectsAllowed"] = false
        };
    }

    private static JObject Template(
        string templateId,
        string narrativeScope,
        IEnumerable<string> requiredActionIds,
        IEnumerable<string> optionalActionIds,
        IEnumerable<JObject> initialGuards,
        IEnumerable<JObject> edges) => new()
    {
        ["templateId"] = templateId,
        ["narrativeScope"] = narrativeScope,
        ["initialGuards"] = new JArray(initialGuards),
        ["requiredActionIds"] = new JArray(requiredActionIds),
        ["optionalActionIds"] = new JArray(optionalActionIds),
        ["edges"] = new JArray(edges)
    };

    private static JObject Edge(
        string fromActionId,
        string toActionId,
        string kind,
        params string[] stateKeys) => new()
    {
        ["fromActionId"] = fromActionId,
        ["toActionId"] = toActionId,
        ["kind"] = kind,
        ["stateKeys"] = new JArray(stateKeys ?? Array.Empty<string>())
    };

    private static JObject Predicate(
        string sourceNodeId,
        string stateKey,
        string comparison,
        object expected) => new()
    {
        ["sourceNodeId"] = sourceNodeId,
        ["stateKey"] = stateKey,
        ["operator"] = comparison,
        ["expected"] = JToken.FromObject(expected)
    };

    private static void ValidateCatalog(JObject catalog, string gameCommit)
    {
        int schemaVersion = (int)catalog["schemaVersion"];
        bool modern = schemaVersion >= 2;
        bool companionAware = schemaVersion >= 3;
        string[] catalogFields = modern
            ? new[]
            {
                "schemaVersion", "schema", "kind", "version", "catalogVersion",
                "gameCommit", "authority", "naturalPlayClaimed",
                "humanApprovalClaimed", "trainingEligible", "subjectRoles",
                "stateKeys", "actions", "scenarioTemplates"
            }
            : new[]
            {
                "schemaVersion", "schema", "kind", "version", "catalogVersion",
                "gameCommit", "authority", "naturalPlayClaimed",
                "humanApprovalClaimed", "trainingEligible", "stateKeys", "actions",
                "scenarioTemplates"
            };
        RequireFields(catalog, "catalog", catalogFields);
        Require((schemaVersion == 4 && (string)catalog["schema"] == CatalogSchema)
            || (schemaVersion == 3 && (string)catalog["schema"] == AtomicCatalogSchema)
            || (schemaVersion == 2
                && (string)catalog["schema"] == PreviousCatalogSchema)
            || (schemaVersion == 1
                && (string)catalog["schema"] == LegacyCatalogSchema),
            "Unsupported action catalog schema.");
        Require((schemaVersion == 4 && (string)catalog["catalogVersion"] == CatalogVersion)
            || (schemaVersion is 2 or 3 && (string)catalog["catalogVersion"] is "causal-actions-v2" or "causal-actions-v3")
            || (!modern
                && (string)catalog["catalogVersion"] == "causal-actions-v1"),
            "Unexpected action catalog version.");
        Require((string)catalog["gameCommit"] == gameCommit,
            "Action catalog game commit does not match current source.");
        Require(!(bool)catalog["naturalPlayClaimed"]
            && !(bool)catalog["humanApprovalClaimed"]
            && !(bool)catalog["trainingEligible"],
            "Action catalog approval gates must remain false.");
        var knownSubjectRoles = new HashSet<string>(StringComparer.Ordinal);
        if (modern)
        {
            JArray subjectRoles = catalog["subjectRoles"] as JArray
                ?? throw new InvalidOperationException(
                    "Catalog subjectRoles must be an array.");
            Require(subjectRoles.Count > 0, "Catalog subjectRoles cannot be empty.");
            foreach (JObject role in subjectRoles.Cast<JObject>())
            {
                RequireFields(
                    role,
                    "catalog subject role",
                    "roleId", "kind", "binding", "profile");
                string roleId = CausalGameplaySyntax.RequireId(
                    (string)role["roleId"],
                    "catalog subject role");
                Require(knownSubjectRoles.Add(roleId),
                    "Duplicate catalog subject role: " + roleId);
                Require((string)role["kind"] is "character" or "facility" or "faction" or "item"
                        or "knowledge-task" or "dungeon-space"
                         or "research-project" or "room" or "species-definition"
                         or "region" or "policy" or "wildlife"
                         or "disease-definition" or "expedition"
                         or "expedition-target" or "operation",
                    "Unsupported subject role kind: " + roleId);
                Require((string)role["binding"] is "plan-derived" or "fixture-owned",
                    "Unsupported subject role binding: " + roleId);
                Require(!string.IsNullOrWhiteSpace((string)role["profile"]),
                    "Subject role profile is required: " + roleId);
            }
        }
        JArray stateKeys = catalog["stateKeys"] as JArray
            ?? throw new InvalidOperationException("Catalog stateKeys must be an array.");
        var knownStateKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (JObject stateKey in stateKeys.Cast<JObject>())
        {
            RequireFields(stateKey, "catalog state key", "id", "type", "authority");
            string stateKeyId = (string)stateKey["id"];
            CausalGameplaySyntax.RequireStateKey(stateKeyId, "catalog state key");
            Require(knownStateKeys.Add(stateKeyId),
                "Duplicate catalog state key: " + stateKeyId);
            Require((string)stateKey["type"] is "string" or "number" or "bool",
                "Unsupported catalog state-key type: " + stateKeyId);
            Require((string)stateKey["authority"] == "observed-from-gameplay-owner",
                "Catalog state key has a non-owner authority: " + stateKeyId);
        }
        JArray actions = catalog["actions"] as JArray
            ?? throw new InvalidOperationException("Catalog actions must be an array.");
        Require(actions.Count > 0, "Action catalog is empty.");
        foreach (JObject action in actions.Cast<JObject>())
        {
            string[] actionFields = modern
                ? new[]
                {
                    "actionId", "domainId", "productionEntry", "argumentSchema",
                    "outcomeKind", "outcomeTypeId", "observedStateKeys",
                    "subjectRoleIds", "narrativeFunctionIds", "maxUsesPerPlan",
                    "hypotheticalEffectsAllowed"
                }
                : new[]
                {
                    "actionId", "domainId", "productionEntry", "argumentSchema",
                    "outcomeKind", "outcomeTypeId", "observedStateKeys",
                    "hypotheticalEffectsAllowed"
                };
            if (companionAware)
                actionFields = actionFields.Concat(new[] { "companionOutcomes" }).ToArray();
            RequireFields(action, "catalog action", actionFields);
            Require(action["argumentSchema"] is JObject,
                "Catalog action argumentSchema must be an object.");
            Require(!(bool)action["hypotheticalEffectsAllowed"],
                "Catalog action cannot authorize hypothetical effects.");
            if (companionAware)
            {
                JArray companions = action["companionOutcomes"] as JArray
                    ?? throw new InvalidOperationException(
                        "Catalog action companionOutcomes must be an array.");
                var companionTypes = new HashSet<string>(StringComparer.Ordinal);
                foreach (JObject companion in companions.Cast<JObject>())
                {
                    RequireFields(
                        companion,
                        "catalog companion outcome",
                        schemaVersion == 4 ? new[] { "outcomeKind", "outcomeTypeId", "relation" }
                            : new[] { "outcomeKind", "outcomeTypeId" });
                    if (schemaVersion == 4)
                        Require((string)companion["relation"] is "atomic-sibling" or "post-commit-observer" or "post-commit-continuation",
                            "Unsupported catalog companion relation.");
                    string companionKind = CausalGameplaySyntax.RequireId(
                        (string)companion["outcomeKind"],
                        "catalog companion outcome kind");
                    string companionType = CausalGameplaySyntax.RequireId(
                        (string)companion["outcomeTypeId"],
                        "catalog companion outcome type");
                    Require(companionKind.Length > 0
                            && companionType != (string)action["outcomeTypeId"]
                            && companionTypes.Add(companionType),
                        "Catalog action companion outcomes must be distinct and non-primary: "
                        + (string)action["actionId"]);
                }
            }
            foreach (string observed in RequireStringArray(
                         action["observedStateKeys"],
                         "observedStateKeys"))
            {
                Require(knownStateKeys.Contains(observed),
                    "Catalog action references an unknown state key: " + observed);
            }
            if (modern)
            {
                string[] roleIds = RequireStringArray(
                    action["subjectRoleIds"],
                    "subjectRoleIds");
                Require(roleIds.Length > 0
                    && roleIds.Distinct(StringComparer.Ordinal).Count()
                        == roleIds.Length
                    && roleIds.All(knownSubjectRoles.Contains),
                    "Catalog action has missing, duplicate or unknown subject roles: "
                    + (string)action["actionId"]);
                string[] functions = RequireStringArray(
                    action["narrativeFunctionIds"],
                    "narrativeFunctionIds");
                Require(functions.Length > 0
                    && functions.Distinct(StringComparer.Ordinal).Count()
                        == functions.Length,
                    "Catalog action narrative functions are invalid: "
                    + (string)action["actionId"]);
                Require((int)action["maxUsesPerPlan"] == 1,
                    "Current production fixtures allow exactly one use per plan: "
                    + (string)action["actionId"]);
            }
        }
        string[] ids = actions.Select(value => (string)value["actionId"]).ToArray();
        Require(ids.All(value => !string.IsNullOrWhiteSpace(value))
            && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length,
            "Catalog action IDs must be unique and non-empty.");
        ValidateCatalogTemplates(
            catalog["scenarioTemplates"],
            ids,
            knownStateKeys,
            modern);
    }

    private static void ValidateCatalogTemplates(
        JToken token,
        IReadOnlyCollection<string> actionIds,
        ISet<string> stateKeys,
        bool modern)
    {
        JArray templates = token as JArray
            ?? throw new InvalidOperationException(
                "Catalog scenarioTemplates must be an array.");
        Require(templates.Count > 0, "Catalog scenarioTemplates cannot be empty.");
        var knownActions = new HashSet<string>(actionIds, StringComparer.Ordinal);
        var templateIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JObject template in templates.Cast<JObject>())
        {
            if (!modern)
            {
                RequireFields(
                    template,
                    "legacy scenario template",
                    "templateId", "initialGuards", "requiredActionIds");
                continue;
            }
            RequireFields(
                template,
                "scenario template",
                "templateId", "narrativeScope", "initialGuards",
                "requiredActionIds", "optionalActionIds", "edges");
            string templateId = CausalGameplaySyntax.RequireId(
                (string)template["templateId"],
                "scenario template");
            Require(templateIds.Add(templateId),
                "Duplicate scenario template: " + templateId);
            Require(!string.IsNullOrWhiteSpace((string)template["narrativeScope"]),
                "Scenario template narrative scope is required: " + templateId);
            string[] required = RequireStringArray(
                template["requiredActionIds"],
                "requiredActionIds");
            string[] optional = RequireStringArray(
                template["optionalActionIds"],
                "optionalActionIds");
            string[] allowed = required.Concat(optional).ToArray();
            Require(required.Length > 0
                && allowed.Distinct(StringComparer.Ordinal).Count() == allowed.Length
                && allowed.All(knownActions.Contains),
                "Scenario template action membership is invalid: " + templateId);
            foreach (JObject guard in ((JArray)template["initialGuards"]).Cast<JObject>())
            {
                RequireFields(
                    guard,
                    "scenario initial guard",
                    "sourceNodeId", "stateKey", "operator", "expected");
                Require((string)guard["sourceNodeId"] == "initial"
                    && stateKeys.Contains((string)guard["stateKey"]),
                    "Scenario initial guard is not bound to an authoritative state key: "
                    + templateId);
                _ = ParseOperator((string)guard["operator"]);
            }
            foreach (JObject edge in ((JArray)template["edges"]).Cast<JObject>())
            {
                RequireFields(
                    edge,
                    "scenario edge",
                    "fromActionId", "toActionId", "kind", "stateKeys");
                string from = (string)edge["fromActionId"];
                string to = (string)edge["toActionId"];
                string kind = (string)edge["kind"];
                string[] edgeStateKeys = RequireStringArrayAllowEmpty(
                    edge["stateKeys"],
                    "edge stateKeys");
                Require(from != to
                    && allowed.Contains(from, StringComparer.Ordinal)
                    && allowed.Contains(to, StringComparer.Ordinal)
                    && kind is "observed-state" or "committed-outcome" or "ordering-only",
                    "Scenario edge is invalid: " + templateId);
                Require((kind == "observed-state" && edgeStateKeys.Length > 0)
                    || (kind != "observed-state" && edgeStateKeys.Length == 0),
                    "Only observed-state edges may name state keys: " + templateId);
                Require(edgeStateKeys.All(stateKeys.Contains),
                    "Scenario edge references an unknown state key: " + templateId);
            }
        }
    }

    private static CausalGameplayPlan ParsePlan(
        JObject json,
        JObject catalog,
        string catalogSha256,
        string gameCommit)
    {
        int schemaVersion = (int)json["schemaVersion"];
        bool modern = schemaVersion >= 2;
        string[] planFields = modern
            ? new[]
            {
                "schemaVersion", "schema", "kind", "planId", "revision",
                "parentPlanSha256", "catalogVersion", "catalogSha256", "gameCommit",
                "templateId", "seed", "humanApprovalClaimed", "trainingEligible",
                "topologicalOrder", "nodes", "planSha256"
            }
            : new[]
            {
                "schemaVersion", "schema", "kind", "planId", "revision",
                "parentPlanSha256", "catalogVersion", "catalogSha256", "gameCommit",
                "humanApprovalClaimed", "trainingEligible", "topologicalOrder",
                "nodes", "planSha256"
            };
        RequireFields(json, "plan", planFields);
        Require((schemaVersion == 2 && (string)json["schema"] == PlanSchema)
            || (schemaVersion == 1
                && (string)json["schema"] == LegacyPlanSchema),
            "Unsupported causal gameplay plan schema.");
        Require(modern == ((int)catalog["schemaVersion"] >= 2),
            "Plan and catalog schema generations must match.");
        Require((string)json["kind"] == "unity-causal-gameplay-plan",
            "Unexpected plan kind.");
        Require(!(bool)json["humanApprovalClaimed"]
            && !(bool)json["trainingEligible"],
            "Plan approval gates must remain false.");
        Require((string)json["catalogVersion"]
                == (string)catalog["catalogVersion"],
            "Plan catalog version mismatch.");
        Require((string)json["catalogSha256"] == catalogSha256,
            "Plan catalog SHA-256 mismatch.");
        Require((string)json["gameCommit"] == gameCommit
            && (string)catalog["gameCommit"] == gameCommit,
            "Plan game commit mismatch.");

        string declaredPlanHash = (string)json["planSha256"];
        JObject hashInput = (JObject)json.DeepClone();
        hashInput.Remove("planSha256");
        string actualPlanHash = CausalGameplayCanonicalJson.Sha256(hashInput);
        Require(string.Equals(
                declaredPlanHash,
                actualPlanHash,
                StringComparison.Ordinal),
            "Plan SHA-256 mismatch.");

        JArray nodesJson = json["nodes"] as JArray
            ?? throw new InvalidOperationException("Plan nodes must be an array.");
        var nodes = new List<CausalGameplayPlanNode>(nodesJson.Count);
        foreach (JObject node in nodesJson.Cast<JObject>())
        {
            string[] nodeFields = modern
                ? new[]
                {
                    "nodeId", "actionId", "arguments", "dependsOn",
                    "causalLinks", "guards", "expectedState"
                }
                : new[]
                {
                    "nodeId", "actionId", "arguments", "dependsOn", "guards",
                    "expectedState"
                };
            RequireFields(node, "plan node", nodeFields);
            nodes.Add(new CausalGameplayPlanNode(
                (string)node["nodeId"],
                (string)node["actionId"],
                node["arguments"] as JObject
                    ?? throw new InvalidOperationException("Node arguments must be an object."),
                RequireStringArray(node["dependsOn"], "dependsOn"),
                ParsePredicates(node["guards"], "guards"),
                ParsePredicates(node["expectedState"], "expectedState"),
                modern
                    ? ParseCausalLinks(node["causalLinks"])
                    : null));
        }
        var plan = new CausalGameplayPlan(
            (string)json["planId"],
            (int)json["revision"],
            declaredPlanHash,
            (string)json["parentPlanSha256"],
            (string)json["catalogVersion"],
            (string)json["catalogSha256"],
            nodes,
            RequireStringArray(json["topologicalOrder"], "topologicalOrder"),
            schemaVersion,
            modern ? (string)json["templateId"] : string.Empty,
            modern ? (int)json["seed"] : 0);
        if (modern)
            ValidatePlanRecipe(plan, catalog);
        return plan;
    }

    private static IReadOnlyList<CausalGameplayCausalLink> ParseCausalLinks(
        JToken token)
    {
        JArray values = token as JArray
            ?? throw new InvalidOperationException("causalLinks must be an array.");
        return values.Cast<JObject>().Select(value =>
        {
            RequireFields(
                value,
                "causal link",
                "parentNodeId", "kind", "stateKeys");
            return new CausalGameplayCausalLink(
                (string)value["parentNodeId"],
                ParseLinkKind((string)value["kind"]),
                RequireStringArrayAllowEmpty(value["stateKeys"], "stateKeys"));
        }).ToArray();
    }

    private static CausalGameplayLinkKind ParseLinkKind(string value) =>
        value switch
        {
            "observed-state" => CausalGameplayLinkKind.ObservedState,
            "committed-outcome" => CausalGameplayLinkKind.CommittedOutcome,
            "ordering-only" => CausalGameplayLinkKind.OrderingOnly,
            _ => throw new InvalidOperationException(
                "Unsupported causal link kind '" + value + "'.")
        };

    private static void ValidatePlanRecipe(
        CausalGameplayPlan plan,
        JObject catalog)
    {
        JObject template = ((JArray)catalog["scenarioTemplates"])
            .Cast<JObject>()
            .SingleOrDefault(value => string.Equals(
                (string)value["templateId"],
                plan.TemplateId,
                StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "Plan references an unknown scenario template: "
                + plan.TemplateId);
        string[] required = RequireStringArray(
            template["requiredActionIds"],
            "requiredActionIds");
        var allowed = new HashSet<string>(
            required.Concat(RequireStringArray(
                template["optionalActionIds"],
                "optionalActionIds")),
            StringComparer.Ordinal);
        Dictionary<string, CausalGameplayPlanNode> byAction = plan.Nodes
            .ToDictionary(node => node.ActionId, StringComparer.Ordinal);
        Require(required.All(byAction.ContainsKey)
            && byAction.Keys.All(allowed.Contains),
            "Plan action membership violates its C# scenario recipe.");

        foreach (JObject expectedGuard in
                 ((JArray)template["initialGuards"]).Cast<JObject>())
        {
            CausalGameplayPredicateOperator comparison = ParseOperator(
                (string)expectedGuard["operator"]);
            bool found = plan.Nodes.SelectMany(node => node.Guards).Any(guard =>
                guard.SourceNodeId == "initial"
                && guard.StateKey == (string)expectedGuard["stateKey"]
                && guard.Comparison == comparison
                && JToken.DeepEquals(
                    guard.Expected,
                    expectedGuard["expected"]));
            Require(found,
                "Plan omitted a required C# recipe initial guard: "
                + (string)expectedGuard["stateKey"]);
        }

        var expectedEdges = new Dictionary<(string From, string To), JObject>();
        foreach (JObject edge in ((JArray)template["edges"]).Cast<JObject>())
        {
            string fromAction = (string)edge["fromActionId"];
            string toAction = (string)edge["toActionId"];
            if (!byAction.ContainsKey(fromAction) || !byAction.ContainsKey(toAction))
                continue;
            expectedEdges.Add((fromAction, toAction), edge);
        }

        foreach (CausalGameplayPlanNode node in plan.Nodes)
        {
            foreach (string parentId in node.DependencyNodeIds)
            {
                CausalGameplayPlanNode parent = plan.Nodes.Single(value =>
                    value.NodeId == parentId);
                Require(expectedEdges.TryGetValue(
                        (parent.ActionId, node.ActionId),
                        out JObject expectedEdge),
                    "Plan introduced an edge outside the C# scenario recipe.");
                CausalGameplayCausalLink link = node.CausalLinks.Single(value =>
                    value.ParentNodeId == parentId);
                Require(link.Kind == ParseLinkKind((string)expectedEdge["kind"])
                    && link.StateKeys.SequenceEqual(
                        RequireStringArrayAllowEmpty(
                            expectedEdge["stateKeys"],
                            "edge stateKeys"),
                        StringComparer.Ordinal),
                    "Plan changed the kind or state keys of a C# scenario edge.");
            }
        }
        Require(expectedEdges.Count == plan.Nodes.Sum(node =>
            node.DependencyNodeIds.Count),
            "Plan omitted an induced edge from its C# scenario recipe.");
    }

    private static IReadOnlyList<CausalGameplayPredicate> ParsePredicates(
        JToken token,
        string label)
    {
        JArray values = token as JArray
            ?? throw new InvalidOperationException(label + " must be an array.");
        return values.Cast<JObject>().Select(value =>
        {
            RequireFields(
                value,
                label + " predicate",
                "sourceNodeId", "stateKey", "operator", "expected");
            return new CausalGameplayPredicate(
                (string)value["sourceNodeId"],
                (string)value["stateKey"],
                ParseOperator((string)value["operator"]),
                value["expected"]);
        }).ToArray();
    }

    private static CausalGameplayPredicateOperator ParseOperator(string value) =>
        value switch
        {
            "equal" => CausalGameplayPredicateOperator.Equal,
            "not-equal" => CausalGameplayPredicateOperator.NotEqual,
            "less" => CausalGameplayPredicateOperator.Less,
            "less-or-equal" => CausalGameplayPredicateOperator.LessOrEqual,
            "greater" => CausalGameplayPredicateOperator.Greater,
            "greater-or-equal" => CausalGameplayPredicateOperator.GreaterOrEqual,
            _ => throw new InvalidOperationException(
                "Unsupported causal predicate operator '" + value + "'.")
        };

    private static void ValidatePlanActionsMatchCatalog(
        CausalGameplayPlan plan,
        JObject catalog,
        CausalGameplayActionRegistry registry)
    {
        Dictionary<string, JObject> catalogActions = ((JArray)catalog["actions"])
            .Cast<JObject>()
            .ToDictionary(value => (string)value["actionId"], StringComparer.Ordinal);
        foreach (CausalGameplayPlanNode node in plan.Nodes)
        {
            Require(catalogActions.ContainsKey(node.ActionId),
                "Plan action is absent from the pinned catalog: " + node.ActionId);
            ICausalGameplayActionHandler handler = registry.Require(node.ActionId);
            if (handler is FixtureActionHandler fixtureHandler)
                fixtureHandler.ValidateCatalogAction(catalogActions[node.ActionId], (int)catalog["schemaVersion"]);
        }
    }

    private static string RunIdForPlan(CausalGameplayPlan plan) =>
        CausalGameplaySyntax.RequireId(
            "run:causal-plan:" + plan.PlanSha256.Substring("sha256:".Length),
            nameof(plan));

    private static CausalGameplayActionRegistry CreateRegistry(
        ProductionPlanWorld world)
    {
        var handlers = new List<ICausalGameplayActionHandler>
        {
            new DrinkCleanWaterHandler(world),
            new RestHandler(world),
            new TrainProficiencyHandler(world)
        };
        handlers.AddRange(world.Fixtures.Select(fixture =>
            (ICausalGameplayActionHandler)new FixtureActionHandler(
                world,
                fixture)));
        return new CausalGameplayActionRegistry(handlers);
    }

    private static bool HasOutcomeRelations(CausalGameplayPlan plan) => plan.CatalogVersion == CatalogVersion;
    private static int ResultVersionForPlan(CausalGameplayPlan plan) => HasOutcomeRelations(plan) ? 5 : 4;
    private static string ResultSchemaForPlan(CausalGameplayPlan plan) => HasOutcomeRelations(plan) ? ResultSchema : AtomicResultSchema;

    private static JObject ResultJson(
        CausalGameplayPlan plan,
        CausalGameplayExecutionResult result,
        string catalogSha256) => new()
    {
        ["schemaVersion"] = ResultVersionForPlan(plan),
        ["schema"] = ResultSchemaForPlan(plan),
        ["kind"] = "unity-causal-gameplay-execution-result",
        ["planId"] = plan.PlanId,
        ["planRevision"] = plan.Revision,
        ["planSha256"] = plan.PlanSha256,
        ["catalogVersion"] = plan.CatalogVersion,
        ["catalogSha256"] = catalogSha256,
        ["status"] = result.ReplanRequired ? "replan-required" : "completed",
        ["replanRequired"] = result.ReplanRequired,
        ["naturalPlayClaimed"] = false,
        ["humanApprovalClaimed"] = false,
        ["trainingEligible"] = false,
        ["records"] = new JArray(result.Records.Select(record =>
            RecordJson(record, relationshipAware: HasOutcomeRelations(plan)))),
        ["invalidatedNodeIds"] = new JArray(result.InvalidatedNodeIds),
        ["finalState"] = result.FinalState.DeepClone(),
        ["finalStateSha256"] = result.FinalStateSha256
    };

    private static JObject RecordJson(
        CausalGameplayNodeRecord record,
        bool requireOutcomeEvidence = true,
        bool relationshipAware = false)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));
        bool hasOutcome = !string.IsNullOrEmpty(record.OutcomeId);
        if (requireOutcomeEvidence && hasOutcome)
            ValidateOutcomeEvidence(record);
        if (requireOutcomeEvidence && relationshipAware)
            ValidateOutcomeRelations(record);
        var json = new JObject
        {
            ["nodeId"] = record.NodeId,
            ["actionId"] = record.ActionId,
            ["status"] = record.Status.ToString(),
            ["preState"] = record.Before?.DeepClone(),
            ["preStateSha256"] = record.BeforeSha256,
            ["postState"] = record.After?.DeepClone(),
            ["postStateSha256"] = record.AfterSha256,
            ["outcomeId"] = record.OutcomeId,
            ["outcomeTypeId"] = record.OutcomeTypeId,
            ["companionOutcomeEvidence"] = requireOutcomeEvidence
                ? record.CompanionOutcomeEvidence?.DeepClone() ?? new JArray()
                : null,
            ["failureCode"] = record.FailureCode,
            ["failureReason"] = record.FailureReason,
            ["mismatches"] = new JArray(record.MismatchReasons)
        };
        if (requireOutcomeEvidence)
        {
            json["outcomeEvidence"] = record.OutcomeEvidence?.DeepClone();
            if (relationshipAware)
                json["companionOutcomeRelations"] = record.CompanionOutcomeRelations.DeepClone();
        }
        else
            json.Remove("companionOutcomeEvidence");
        return json;
    }

    private static void ValidateOutcomeRelations(CausalGameplayNodeRecord record, JArray declarations = null)
    {
        JArray relations = record.CompanionOutcomeRelations;
        Require(relations != null && relations.Count == record.CompanionOutcomeEvidence.Count,
            "Companion relation cardinality changed for " + record.NodeId);
        Require(!string.IsNullOrEmpty(record.OutcomeId) || relations.Count == 0,
            "Uncommitted record cannot claim companion relations.");
        if (declarations != null)
            Require(declarations.Count == relations.Count, "Catalog companion relation count changed.");
        for (int index = 0; index < relations.Count; index++)
        {
            JObject relation = relations[index] as JObject;
            RequireFields(relation, "companion outcome relation", "outcomeId", "relation", "relatedOutcomeId");
            JObject companion = record.CompanionOutcomeEvidence[index] as JObject
                ?? throw new InvalidOperationException("Companion evidence must be an object.");
            string kind = (string)relation["relation"];
            Require(kind is "atomic-sibling" or "post-commit-observer" or "post-commit-continuation"
                && (string)relation["outcomeId"] == (string)companion["outcomeId"]
                && (string)relation["relatedOutcomeId"] == record.OutcomeId
                && (string)relation["outcomeId"] != record.OutcomeId,
                "Companion outcome relation identity changed.");
            if (declarations != null)
                Require(kind == (string)declarations[index]["relation"]
                    && (string)companion["snapshot"]?["outcomeTypeId"] == (string)declarations[index]["outcomeTypeId"],
                    "Companion outcome relation differs from pinned catalog.");
            if (kind is "post-commit-observer" or "post-commit-continuation")
            {
                JToken primary = record.OutcomeEvidence?["snapshot"];
                JToken observed = companion["snapshot"];
                Require(primary != null && observed != null
                    && !string.IsNullOrWhiteSpace((string)primary["runId"])
                    && (string)primary["runId"] == (string)observed["runId"]
                    && primary["sequence"]?.Type == JTokenType.Integer
                    && observed["sequence"]?.Type == JTokenType.Integer
                    && (long)primary["sequence"] > 0
                    && (long)observed["sequence"] > (long)primary["sequence"],
                    "Post-commit companion must follow its primary in the same exact ledger.");
            }
        }
    }

    // Contract probe over real production evidence; not an extra gameplay action.
    internal static JObject VerifyOwnerCompletionRelationContract(JObject report)
    {
        var ordered = (JArray)report["orderedOutcomes"];
        var primary = (JObject)ordered[0]["outcomeEvidence"];
        var observer = (JObject)ordered[1]["outcomeEvidence"];
        var relations = new JArray(new JObject
        {
            ["outcomeId"] = (string)observer["outcomeId"],
            ["relation"] = "post-commit-observer",
            ["relatedOutcomeId"] = (string)primary["outcomeId"]
        });
        var declarations = new JArray(new JObject
        {
            ["outcomeKind"] = MigratedProducerOutcomeKind.RunResultReadyEvent.ToString(),
            ["outcomeTypeId"] = (string)observer["snapshot"]["outcomeTypeId"],
            ["relation"] = "post-commit-observer"
        });
        CausalGameplayNodeRecord Record(JArray values, JObject companion) => new(
            new CausalGameplayPlanNode("node:owner-completion", OwnerRunCompletionCausalProductionEntryDebugScenarios.CompleteActionId,
                new JObject(), Array.Empty<string>(), Array.Empty<CausalGameplayPredicate>(), Array.Empty<CausalGameplayPredicate>()),
            CausalGameplayNodeStatus.Completed, (JObject)report["initial"]["domainState"], (JObject)report["first"]["domainState"],
            new CausalGameplayActionResult(true, (string)primary["outcomeId"], (string)primary["snapshot"]["outcomeTypeId"],
                string.Empty, string.Empty, primary, new JArray(companion.DeepClone()), values), Array.Empty<string>());
        CausalGameplayNodeRecord good = Record(relations, observer);
        ValidateOutcomeRelations(good, declarations);
        JObject json = RecordJson(good, relationshipAware: true);
        Require(JToken.DeepEquals(json["companionOutcomeRelations"], relations), "Relation metadata lost in record serialization.");
        Require(RecordJson(good)["companionOutcomeRelations"] == null, "Legacy record shape changed.");
        var rejected = new JArray();
        foreach (string mutation in new[] { "missing", "wrong-child", "wrong-parent", "atomic-relabel", "unknown-kind", "reversed-sequence", "different-run" })
        {
            var changed = (JArray)relations.DeepClone();
            var evidence = (JObject)observer.DeepClone();
            switch (mutation)
            {
                case "missing": changed.Clear(); break;
                case "wrong-child": changed[0]["outcomeId"] = "outcome:wrong"; break;
                case "wrong-parent": changed[0]["relatedOutcomeId"] = "outcome:wrong"; break;
                case "atomic-relabel": changed[0]["relation"] = "atomic-sibling"; break;
                case "unknown-kind": changed[0]["relation"] = "implied"; break;
                case "reversed-sequence": evidence["snapshot"]["sequence"] = 1; break;
                case "different-run": evidence["snapshot"]["runId"] = "run:wrong"; break;
            }
            bool failed = false;
            try { ValidateOutcomeRelations(Record(changed, evidence), declarations); }
            catch (InvalidOperationException) { failed = true; }
            Require(failed, "Tampered outcome relation was accepted: " + mutation);
            rejected.Add(mutation);
        }
        JObject catalog = BuildCatalog("relation-contract-probe", "controlled-contract-test");
        ValidateCatalog(catalog, "controlled-contract-test");
        return new JObject { ["status"] = "PASS", ["tamperCases"] = rejected,
            ["serializedActualRecord"] = json, ["catalogVersion"] = CatalogVersion,
            ["catalogActionCount"] = ((JArray)catalog["actions"]).Count,
            ["scope"] = "relation-contract-over-actual-owner-results-not-campaign-integration" };
    }

    private static void ValidateOutcomeEvidence(CausalGameplayNodeRecord record)
    {
        JObject evidence = record.OutcomeEvidence
            ?? throw new InvalidOperationException(
                "A durable causal record is missing exact outcome evidence: "
                + record.NodeId);
        int evidenceSchemaVersion = (int)evidence["schemaVersion"];
        RequireFields(
            evidence,
            "causal outcome evidence",
            evidenceSchemaVersion >= 2
                ? new[]
                {
                    "schemaVersion", "schema", "outcomeId", "resultKey",
                    "replayState", "canonicalPayloadHash", "snapshotJson",
                    "snapshotSha256", "snapshot"
                }
                : new[]
                {
                    "schemaVersion", "schema", "outcomeId", "resultKey",
                    "replayState", "canonicalPayloadHash", "snapshotSha256",
                    "snapshot"
                });
        JObject snapshot = evidence["snapshot"] as JObject
            ?? throw new InvalidOperationException(
                "Causal outcome evidence snapshot must be an object.");
        bool legacyHashValid = evidenceSchemaVersion == 1
            && (string)evidence["schema"]
                == "dungeonstory-causal-outcome-evidence@1"
            && (string)evidence["snapshotSha256"]
                == CausalGameplayCanonicalJson.Sha256(snapshot);
        string snapshotJsonText = evidenceSchemaVersion >= 2
            ? (string)evidence["snapshotJson"]
            : string.Empty;
        bool portableHashValid = evidenceSchemaVersion == 2
            && (string)evidence["schema"]
                == "dungeonstory-causal-outcome-evidence@2"
            && !string.IsNullOrEmpty(snapshotJsonText)
            && (string)evidence["snapshotSha256"]
                == ProductionCausalOutcomeEvidence.Sha256Text(snapshotJsonText)
            && JToken.DeepEquals(
                snapshot,
                ProductionCausalOutcomeEvidence.ParseSnapshotJson(
                    snapshotJsonText));
        Require((legacyHashValid || portableHashValid)
            && (string)evidence["outcomeId"] == record.OutcomeId
            && (string)snapshot["outcomeTypeId"] == record.OutcomeTypeId
            && (string)evidence["canonicalPayloadHash"]
                == (string)snapshot["immutablePayloadHash"],
            "Causal outcome evidence identity or hash mismatch for "
            + record.NodeId + ".");
        var companionTypes = new HashSet<string>(StringComparer.Ordinal);
        var companionIds = new HashSet<string>(StringComparer.Ordinal)
        {
            record.OutcomeId
        };
        foreach (JObject companion in record.CompanionOutcomeEvidence
                     .OfType<JObject>())
        {
            ValidateOutcomeEvidence(
                companion,
                (string)companion["outcomeId"],
                (string)companion["snapshot"]?["outcomeTypeId"],
                record.NodeId + " companion");
            string companionId = (string)companion["outcomeId"];
            string companionType = (string)companion["snapshot"]?["outcomeTypeId"];
            Require(companionIds.Add(companionId)
                    && companionType != record.OutcomeTypeId
                    && companionTypes.Add(companionType),
                "Causal companion outcome identity or type is duplicated for "
                + record.NodeId + ".");
        }
    }

    private static void ValidateOutcomeEvidence(
        JObject evidence,
        string expectedOutcomeId,
        string expectedOutcomeTypeId,
        string label)
    {
        if (evidence == null)
            throw new InvalidOperationException(label + " evidence is missing.");
        int evidenceSchemaVersion = (int)evidence["schemaVersion"];
        RequireFields(
            evidence,
            label + " evidence",
            evidenceSchemaVersion >= 2
                ? new[]
                {
                    "schemaVersion", "schema", "outcomeId", "resultKey",
                    "replayState", "canonicalPayloadHash", "snapshotJson",
                    "snapshotSha256", "snapshot"
                }
                : new[]
                {
                    "schemaVersion", "schema", "outcomeId", "resultKey",
                    "replayState", "canonicalPayloadHash", "snapshotSha256",
                    "snapshot"
                });
        JObject snapshot = evidence["snapshot"] as JObject
            ?? throw new InvalidOperationException(label + " snapshot is missing.");
        bool legacyHashValid = evidenceSchemaVersion == 1
            && (string)evidence["schema"]
                == "dungeonstory-causal-outcome-evidence@1"
            && (string)evidence["snapshotSha256"]
                == CausalGameplayCanonicalJson.Sha256(snapshot);
        string snapshotJsonText = evidenceSchemaVersion >= 2
            ? (string)evidence["snapshotJson"]
            : string.Empty;
        bool portableHashValid = evidenceSchemaVersion == 2
            && (string)evidence["schema"]
                == "dungeonstory-causal-outcome-evidence@2"
            && !string.IsNullOrEmpty(snapshotJsonText)
            && (string)evidence["snapshotSha256"]
                == ProductionCausalOutcomeEvidence.Sha256Text(snapshotJsonText)
            && JToken.DeepEquals(
                snapshot,
                ProductionCausalOutcomeEvidence.ParseSnapshotJson(
                    snapshotJsonText));
        Require((legacyHashValid || portableHashValid)
                && (string)evidence["outcomeId"] == expectedOutcomeId
                && (string)snapshot["outcomeTypeId"] == expectedOutcomeTypeId
                && (string)evidence["canonicalPayloadHash"]
                    == (string)snapshot["immutablePayloadHash"],
            label + " identity or hash mismatch.");
    }

    private static JObject ReplanRequest(
        CausalGameplayPlan plan,
        CausalGameplayExecutionResult result,
        string catalogSha256,
        string gameCommit) => new()
    {
        ["schemaVersion"] = 2,
        ["schema"] = "dungeonstory-causal-gameplay-replan-request@2",
        ["kind"] = "unity-causal-gameplay-replan-request",
        ["previousPlanId"] = plan.PlanId,
        ["previousPlanRevision"] = plan.Revision,
        ["previousPlanSha256"] = plan.PlanSha256,
        ["requiredNextRevision"] = plan.Revision + 1,
        ["catalogVersion"] = plan.CatalogVersion,
        ["catalogSha256"] = catalogSha256,
        ["gameCommit"] = gameCommit,
        ["actualState"] = result.FinalState.DeepClone(),
        ["actualStateSha256"] = result.FinalStateSha256,
        ["acceptedPrefix"] = new JArray(result.Records
            .Where(value => value.Status is CausalGameplayNodeStatus.Completed
                or CausalGameplayNodeStatus.Diverged)
            .Where(value => !string.IsNullOrEmpty(value.OutcomeId))
            .Select(value => new JObject
            {
                ["nodeId"] = value.NodeId,
                ["outcomeId"] = value.OutcomeId,
                ["postStateSha256"] = value.AfterSha256
            })),
        ["invalidatedNodeIds"] = new JArray(result.InvalidatedNodeIds),
        ["humanApprovalClaimed"] = false,
        ["trainingEligible"] = false
    };

    private sealed class ProductionPlanWorld :
        ICausalGameplayExecutionWorld,
        IDisposable
    {
        private readonly Dictionary<string, IProductionCausalActionFixture>
            fixtures = new(StringComparer.Ordinal);
        private readonly HashSet<string> verifiedFixtureOutcomes =
            new(StringComparer.Ordinal);
        private readonly ProductionCausalWorldCheckpoint worldCheckpoint;
        private readonly IReadOnlyDictionary<string, string> fixtureOwnerIds;

        internal ProductionPlanWorld(
            CausalGameplayPlan plan,
            string rejectedActionId = null)
            : this(plan, rejectedActionId, null, null, null, null, null) { }

        internal ProductionPlanWorld(
            CausalGameplayPlan plan,
            string rejectedActionId,
            string runIdOverride,
            string protagonistIdOverride,
            LongHorizonNarrativeExecutionExporter.LongHorizonCheckpoint
                sessionCheckpoint,
            IReadOnlyDictionary<string, string> fixtureOwnerIds = null,
            JObject campaignWorldCheckpoint = null)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            bool campaignBound = !string.IsNullOrWhiteSpace(runIdOverride)
                || !string.IsNullOrWhiteSpace(protagonistIdOverride)
                || sessionCheckpoint != null
                || fixtureOwnerIds != null
                || campaignWorldCheckpoint != null;
            Require(!campaignBound
                    || (!string.IsNullOrWhiteSpace(runIdOverride)
                        && !string.IsNullOrWhiteSpace(protagonistIdOverride)
                        && plan.SchemaVersion >= 2),
                "Campaign-bound causal worlds require v2 plan, run and protagonist IDs.");
            string runId = campaignBound
                ? CausalGameplaySyntax.RequireId(runIdOverride, nameof(runIdOverride))
                : RunIdForPlan(plan);
            string protagonistId = campaignBound
                ? CausalGameplaySyntax.RequireId(
                    protagonistIdOverride,
                    nameof(protagonistIdOverride))
                : "character:causal-protagonist:"
                    + plan.PlanSha256.Substring("sha256:".Length);
            string requestedRejection = rejectedActionId?.Trim() ?? string.Empty;
            Require(requestedRejection.Length == 0
                || requestedRejection == "action:trigger-defense-facility",
                "Unsupported controlled rejection action.");
            Session = sessionCheckpoint != null
                ? LongHorizonNarrativeExecutionExporter.LongHorizonSession
                    .Restore(
                        runId,
                        sessionCheckpoint,
                        protagonistId,
                        "연화",
                        "human")
                : plan.SchemaVersion >= 2
                    ? LongHorizonNarrativeExecutionExporter.LongHorizonSession
                        .Create(runId, protagonistId, "연화", "human")
                    : LongHorizonNarrativeExecutionExporter.LongHorizonSession
                        .Create(runId);
            this.fixtureOwnerIds = fixtureOwnerIds
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
            worldCheckpoint = new ProductionCausalWorldCheckpoint(
                campaignWorldCheckpoint);
            try
            {
                var actionIds = new HashSet<string>(
                    plan.Nodes.Select(node => node.ActionId),
                    StringComparer.Ordinal);
                bool legacyWorldShape = plan.SchemaVersion < 2;
                string[] combatLifeCycleActions =
                {
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .NonlethalCombatInjuryActionId,
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .RecoverBodyHealthActionId,
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .FatalCombatInjuryActionId
                };
                if (plan.SchemaVersion >= 2
                    && combatLifeCycleActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        combatLifeCycleActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = combatLifeCycleActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             MigratedProducerCombatDefenseEntryE2EDebugScenarios
                                 .CreateCausalCombatLifeCycleFixtures(
                                     runId,
                                     protagonistId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             MigratedProducerCombatDefenseEntryE2EDebugScenarios
                                                 .CombatLifeCycleOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] stockSupplyTransformActions =
                {
                    StockSupplyTransformCausalProductionEntryDebugScenarios
                        .SupplyActionId,
                    StockSupplyTransformCausalProductionEntryDebugScenarios
                        .TransformActionId
                };
                if (plan.SchemaVersion >= 2
                    && stockSupplyTransformActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        stockSupplyTransformActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = stockSupplyTransformActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             StockSupplyTransformCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             StockSupplyTransformCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] facilityWishlistProcurementActions =
                {
                    FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                        .BudgetBlockedActionId,
                    FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                        .PurchaseWishlistActionId
                };
                if (plan.SchemaVersion >= 2
                    && facilityWishlistProcurementActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        facilityWishlistProcurementActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId =
                        facilityWishlistProcurementActions.First(
                            actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] observedCareerArcActions =
                {
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .MentorshipAwardActionId,
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .LastLessonActionId,
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .QuietPromotionActionId,
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .FuneralActionId,
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .LineageCompressionActionId
                };
                if (plan.SchemaVersion >= 2
                    && observedCareerArcActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        observedCareerArcActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = observedCareerArcActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             ObservedCareerArcCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             ObservedCareerArcCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] observedMealIncidentResolutionActions =
                {
                    ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                        .ObserveIncidentActionId,
                    ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                        .ResolveIncidentActionId
                };
                if (plan.SchemaVersion >= 2
                    && observedMealIncidentResolutionActions.Any(
                        actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        observedMealIncidentResolutionActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId =
                        observedMealIncidentResolutionActions.First(
                            actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                if (legacyWorldShape
                    || actionIds.Contains("action:resolve-combat-attack"))
                {
                    string ownerId = OwnerIdFor(
                        "action:resolve-combat-attack",
                        campaignBound);
                    Add(MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .CreateCausalCombatFixture(
                            runId,
                            episodeOwnerId: ownerId));
                }
                if (legacyWorldShape
                    || actionIds.Contains("action:trigger-defense-facility"))
                {
                    string ownerId = OwnerIdFor(
                        "action:trigger-defense-facility",
                        campaignBound);
                    Add(MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .CreateCausalFacilityFixture(
                            runId,
                            requestedRejection
                                == "action:trigger-defense-facility"
                                    ? MigratedProducerOutcomeFaultMode.RejectCommit
                                    : MigratedProducerOutcomeFaultMode.None,
                            ownerId,
                            ownerId == null
                                ? null
                                : worldCheckpoint.Find(
                                    "action:trigger-defense-facility",
                                    ownerId)));
                }
                if (legacyWorldShape
                    || actionIds.Contains("action:adjust-faction-trust"))
                {
                    string ownerId = OwnerIdFor(
                        "action:adjust-faction-trust",
                        campaignBound);
                    Add(MigratedProducerRunOffenseEntryE2EDebugScenarios
                        .CreateCausalRelationshipFixture(
                            runId,
                            ownerId,
                            ownerId == null
                                ? null
                                : worldCheckpoint.Find(
                                    "action:adjust-faction-trust",
                                    ownerId)));
                }
                if (legacyWorldShape
                    || actionIds.Contains("action:execute-offense-battle-command"))
                {
                    string ownerId = OwnerIdFor(
                        "action:execute-offense-battle-command",
                        campaignBound);
                    Add(MigratedProducerRunOffenseEntryE2EDebugScenarios
                        .CreateCausalOffenseFixture(runId, ownerId));
                }
                if (legacyWorldShape
                    || actionIds.Contains("action:apply-detox-treatment"))
                {
                    string ownerId = OwnerIdFor(
                        "action:apply-detox-treatment",
                        campaignBound);
                    Add(MigratedProducerCharacterSurvivalEntryE2EDebugScenarios
                        .CreateCausalTreatmentFixture(runId, ownerId));
                }
                if (legacyWorldShape
                    || actionIds.Contains("action:purchase-retail-item"))
                {
                    string ownerId = OwnerIdFor(
                        "action:purchase-retail-item",
                        campaignBound);
                    Add(MigratedProducerEconomyEntryExtendedE2EDebugScenarios
                        .CreateCausalEconomyFixture(
                            runId,
                            ownerId,
                            ownerId == null
                                ? null
                                : worldCheckpoint.Find(
                                    "action:purchase-retail-item",
                                    ownerId)));
                }
                if (actionIds.Contains(
                        "action:environment-crop-irrigation-supplied")
                    || actionIds.Contains(
                        "action:environment-crop-plan-terminal"))
                {
                    string[] cropActions =
                    {
                        "action:environment-crop-irrigation-supplied",
                        "action:environment-crop-plan-terminal"
                    };
                    string ownerId = OwnerIdForFamily(
                        cropActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = cropActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             EnvironmentCropProductionEntryDebugScenarios
                                 .CreateCausalCropFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             EnvironmentCropProductionEntryDebugScenarios
                                                 .CausalCropOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] fireActions =
                {
                    "action:environment-fire-ignite",
                    "action:environment-fire-initial-attack",
                    "action:environment-fire-fuel-ignition",
                    "action:environment-fire-damage-tick",
                    "action:environment-fire-water-suppression"
                };
                if (fireActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        fireActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = fireActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             EnvironmentFirePhysicalProductionEntryDebugScenarios
                                 .CreateCausalFireFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             EnvironmentFirePhysicalProductionEntryDebugScenarios
                                                 .CausalFireOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] roomEnvironmentActions =
                {
                    RoomEnvironmentCausalProductionEntryDebugScenarios
                        .ExperienceActionId,
                    RoomEnvironmentCausalProductionEntryDebugScenarios
                        .ConditionActionId
                };
                if (roomEnvironmentActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        roomEnvironmentActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = roomEnvironmentActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             RoomEnvironmentCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             RoomEnvironmentCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] speciesIncidentActions =
                {
                    EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                        .SpeciesFirstIncidentActionId,
                    EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                        .SpeciesRepeatIncidentActionId
                };
                if (speciesIncidentActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        speciesIncidentActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = speciesIncidentActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                                 .CreateSpeciesCausalFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                                                 .SpeciesCausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] certifiedSeedActions =
                {
                    EnvironmentCropProductionEntryDebugScenarios
                        .CertifiedSeedFirstCompletionActionId,
                    EnvironmentCropProductionEntryDebugScenarios
                        .CertifiedSeedRepeatCompletionActionId
                };
                if (certifiedSeedActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        certifiedSeedActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = certifiedSeedActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             EnvironmentCropProductionEntryDebugScenarios
                                 .CreateCertifiedSeedCausalFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             EnvironmentCropProductionEntryDebugScenarios
                                                 .CertifiedSeedCausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] socialActions =
                {
                    SocialLifeCausalProductionEntryDebugScenarios
                        .AssaultConflictActionId,
                    SocialLifeCausalProductionEntryDebugScenarios
                        .ApologyActionId
                };
                if (socialActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        socialActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = socialActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             SocialLifeCausalProductionEntryDebugScenarios
                                 .CreateCausalSocialFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             SocialLifeCausalProductionEntryDebugScenarios
                                                 .CausalSocialOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] staffResponseActions =
                {
                    StaffRebellionResponseDebugScenarios.CausalIsolateActionId,
                    StaffRebellionResponseDebugScenarios.CausalCalmActionId
                };
                if (staffResponseActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        staffResponseActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = staffResponseActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             StaffRebellionResponseDebugScenarios
                                 .CreateCausalFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             StaffRebellionResponseDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] emergencySuspensionActions =
                {
                    EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                        .FirstSuspensionActionId,
                    EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                        .SecondSuspensionActionId
                };
                if (emergencySuspensionActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        emergencySuspensionActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = emergencySuspensionActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] bossAwardActions =
                {
                    MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                        .FirstAwardActionId,
                    MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                        .SecondAwardActionId
                };
                if (bossAwardActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        bossAwardActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = bossAwardActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] tradeInventoryActions =
                {
                    TradeInventoryCausalProductionEntryDebugScenarios
                        .RelocateActionId,
                    TradeInventoryCausalProductionEntryDebugScenarios
                        .DisposeActionId,
                    TradeInventoryCausalProductionEntryDebugScenarios
                        .WastePolicyActionId
                };
                if (tradeInventoryActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        tradeInventoryActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = tradeInventoryActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             TradeInventoryCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             TradeInventoryCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] diseaseRouteActions =
                {
                    DiseaseRouteExposureCausalProductionEntryDebugScenarios
                        .FirstExposureActionId,
                    DiseaseRouteExposureCausalProductionEntryDebugScenarios
                        .RepeatExposureActionId
                };
                if (diseaseRouteActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        diseaseRouteActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = diseaseRouteActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             DiseaseRouteExposureCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             DiseaseRouteExposureCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] offenseNodeResolutionActions =
                {
                    OffenseNodeResolutionCausalProductionEntryDebugScenarios
                        .ClearApproachActionId,
                    OffenseNodeResolutionCausalProductionEntryDebugScenarios
                        .SecureCacheActionId
                };
                if (offenseNodeResolutionActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        offenseNodeResolutionActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = offenseNodeResolutionActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             OffenseNodeResolutionCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             OffenseNodeResolutionCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] offenseTruthActions =
                {
                    OffenseTruthRevealCausalProductionEntryDebugScenarios
                        .PreliminaryVictoryActionId,
                    OffenseTruthRevealCausalProductionEntryDebugScenarios
                        .RevealTruthActionId
                };
                if (offenseTruthActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        offenseTruthActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = offenseTruthActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             OffenseTruthRevealCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             OffenseTruthRevealCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] workIdentityActions =
                {
                    WorkCompletionIdentityCausalProductionEntryDebugScenarios
                        .FirstHarvestActionId,
                    WorkCompletionIdentityCausalProductionEntryDebugScenarios
                        .NextDayHarvestActionId
                };
                if (workIdentityActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        workIdentityActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = workIdentityActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             WorkCompletionIdentityCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             WorkCompletionIdentityCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] runVariableActions =
                {
                    RunVariableCausalProductionEntryDebugScenarios
                        .ActivateActionId,
                    RunVariableCausalProductionEntryDebugScenarios
                        .ExpireActionId
                };
                if (runVariableActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        runVariableActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = runVariableActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             RunVariableCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             RunVariableCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] visitorCheckoutActions =
                {
                    VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.ReceptionActionId,
                    VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.CheckoutActionId
                };
                if (visitorCheckoutActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(visitorCheckoutActions, actionIds, campaignBound);
                    string selectedActionId = visitorCheckoutActions.First(actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                        VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.CreateFixtures(
                            runId, ownerId, ownerId == null ? null : worldCheckpoint.Find(
                                selectedActionId, VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.RestoreKind,
                                ownerId)))
                        Add(fixture);
                }
                string[] tabooButcheryActions =
                {
                    TabooButcheryCausalProductionEntryDebugScenarios.ControlledDeathActionId,
                    TabooButcheryCausalProductionEntryDebugScenarios.ButcherCorpseActionId
                };
                if (tabooButcheryActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(tabooButcheryActions, actionIds, campaignBound);
                    string selectedActionId = tabooButcheryActions.First(actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                        TabooButcheryCausalProductionEntryDebugScenarios.CreateFixtures(
                            runId, ownerId, ownerId == null ? null : worldCheckpoint.Find(
                                selectedActionId, TabooButcheryCausalProductionEntryDebugScenarios.RestoreKind,
                                ownerId)))
                        Add(fixture);
                }
                string[] offenseArrivalActions =
                {
                    OffenseArrivalCausalProductionEntryDebugScenarios.FinalizeRewardActionId,
                    OffenseArrivalCausalProductionEntryDebugScenarios.SettleArrivalActionId
                };
                if (offenseArrivalActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        offenseArrivalActions, actionIds, campaignBound);
                    string selectedActionId = offenseArrivalActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             OffenseArrivalCausalProductionEntryDebugScenarios.CreateFixtures(
                                 runId,
                                 ownerId,
                                 ownerId == null
                                     ? null
                                     : worldCheckpoint.Find(
                                         selectedActionId,
                                         OffenseArrivalCausalProductionEntryDebugScenarios.RestoreKind,
                                         ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] festivalCelebrationActions =
                {
                    FestivalCelebrationCausalProductionEntryDebugScenarios.FirstCelebrationActionId,
                    FestivalCelebrationCausalProductionEntryDebugScenarios.SecondCelebrationActionId
                };
                if (festivalCelebrationActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        festivalCelebrationActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = festivalCelebrationActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             FestivalCelebrationCausalProductionEntryDebugScenarios.CreateFixtures(
                                 runId,
                                 ownerId,
                                 ownerId == null
                                     ? null
                                     : worldCheckpoint.Find(
                                         selectedActionId,
                                         FestivalCelebrationCausalProductionEntryDebugScenarios.RestoreKind,
                                         ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] ownerRunActions =
                {
                    OwnerRunCompletionCausalProductionEntryDebugScenarios.CompleteActionId,
                    OwnerRunCompletionCausalProductionEntryDebugScenarios.UpgradeActionId
                };
                if (ownerRunActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(ownerRunActions, actionIds, campaignBound);
                    string selectedActionId = ownerRunActions.First(actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                        OwnerRunCompletionCausalProductionEntryDebugScenarios.CreateFixtures(
                            runId, ownerId, ownerId == null ? null : worldCheckpoint.Find(
                                selectedActionId, OwnerRunCompletionCausalProductionEntryDebugScenarios.RestoreKind,
                                ownerId)))
                        Add(fixture);
                }
                string[] metaProgressionActions =
                {
                    MetaProgressionCausalProductionEntryDebugScenarios
                        .RunResultActionId,
                    MetaProgressionCausalProductionEntryDebugScenarios
                        .UpgradeActionId
                };
                if (metaProgressionActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        metaProgressionActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = metaProgressionActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             MetaProgressionCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             MetaProgressionCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] regularCustomerActions =
                {
                    RegularCustomerCausalProductionEntryDebugScenarios
                        .FirstVisitActionId,
                    RegularCustomerCausalProductionEntryDebugScenarios
                        .CandidateVisitActionId,
                    RegularCustomerCausalProductionEntryDebugScenarios
                        .RecruitActionId
                };
                if (regularCustomerActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        regularCustomerActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = regularCustomerActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             RegularCustomerCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             RegularCustomerCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] characterLifeActions =
                {
                    CharacterLifeCausalProductionEntryDebugScenarios
                        .AgeConditionActionId,
                    CharacterLifeCausalProductionEntryDebugScenarios
                        .LifeStageActionId
                };
                if (characterLifeActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        characterLifeActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = characterLifeActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             CharacterLifeCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             CharacterLifeCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] factionRouteActions =
                {
                    FactionRouteCausalProductionEntryDebugScenarios
                        .SettlementActionId,
                    FactionRouteCausalProductionEntryDebugScenarios
                        .ArrivalActionId,
                    FactionRouteCausalProductionEntryDebugScenarios
                        .CargoActionId
                };
                if (factionRouteActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        factionRouteActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = factionRouteActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             FactionRouteCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             FactionRouteCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] consumablesActions =
                {
                    ConsumablesProgressionCausalProductionEntryDebugScenarios
                        .MealActionId,
                    ConsumablesProgressionCausalProductionEntryDebugScenarios
                        .SubstanceActionId
                };
                if (consumablesActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        consumablesActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = consumablesActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             ConsumablesProgressionCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             ConsumablesProgressionCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] mealShortageActions =
                {
                    MealShortageCausalProductionEntryDebugScenarios
                        .FirstMissActionId,
                    MealShortageCausalProductionEntryDebugScenarios
                        .RepeatMissActionId
                };
                if (mealShortageActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        mealShortageActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = mealShortageActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             MealShortageCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             MealShortageCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] primitiveSurvivalActions =
                {
                    PrimitiveSurvivalCausalProductionEntryDebugScenarios
                        .FirstRestActionId,
                    PrimitiveSurvivalCausalProductionEntryDebugScenarios
                        .RepeatRestActionId
                };
                if (primitiveSurvivalActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        primitiveSurvivalActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = primitiveSurvivalActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             PrimitiveSurvivalCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             PrimitiveSurvivalCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] serviceHubDayActions =
                {
                    ServiceHubDayCausalProductionEntryDebugScenarios.VisitActionId,
                    ServiceHubDayCausalProductionEntryDebugScenarios.ModeActionId,
                    ServiceHubDayCausalProductionEntryDebugScenarios.CloseDayActionId
                };
                if (serviceHubDayActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        serviceHubDayActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = serviceHubDayActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             ServiceHubDayCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             ServiceHubDayCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] invasionLifecycleActions =
                {
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionStartActionId,
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionBreachActionId,
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionDamageActionId,
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionFrontCollapseActionId,
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .InvasionResolveActionId
                };
                if (invasionLifecycleActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        invasionLifecycleActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = invasionLifecycleActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             MigratedProducerCombatDefenseEntryE2EDebugScenarios
                                 .CreateInvasionLifecycleCausalFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             MigratedProducerCombatDefenseEntryE2EDebugScenarios
                                                 .InvasionLifecycleCausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] traitActions =
                {
                    AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                        .ManifestActionId,
                    AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                        .ReactionActionId,
                    AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                        .EraseActionId
                };
                if (traitActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        traitActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = traitActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] captivityActions =
                {
                    CaptivityCausalProductionEntryDebugScenarios.InteractionActionId,
                    CaptivityCausalProductionEntryDebugScenarios.MilestoneActionId,
                    CaptivityCausalProductionEntryDebugScenarios.RansomActionId,
                    CaptivityCausalProductionEntryDebugScenarios.EscapeActionId
                };
                if (captivityActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        captivityActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = captivityActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             CaptivityCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             CaptivityCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] equipmentApparelActions =
                {
                    EquipmentApparelCausalProductionEntryDebugScenarios.CraftActionId,
                    EquipmentApparelCausalProductionEntryDebugScenarios.EquipActionId
                };
                if (equipmentApparelActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        equipmentApparelActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = equipmentApparelActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             EquipmentApparelCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             EquipmentApparelCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] researchKnowledgeActions =
                {
                    KnowledgeResearchOutcomeDebugScenarios
                        .CausalResearchProgressActionId,
                    KnowledgeResearchOutcomeDebugScenarios
                        .CausalResearchCompletionActionId
                };
                if (researchKnowledgeActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        researchKnowledgeActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = researchKnowledgeActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             KnowledgeResearchOutcomeDebugScenarios
                                 .CreateCausalResearchFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             KnowledgeResearchOutcomeDebugScenarios
                                                 .CausalResearchOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] productionBillActions =
                {
                    ProductionPreparedOutputFullPersistenceDebugScenarios
                        .CausalProductionCommandActionId,
                    ProductionPreparedOutputFullPersistenceDebugScenarios
                        .CausalProductionCompletionActionId
                };
                if (productionBillActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        productionBillActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = productionBillActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             ProductionPreparedOutputFullPersistenceDebugScenarios
                                 .CreateCausalProductionFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             ProductionPreparedOutputFullPersistenceDebugScenarios
                                                 .CausalProductionOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] facilityEvolutionActions =
                {
                    FacilityEvolutionDebugScenarios
                        .CausalFacilityEvolutionFirstActionId,
                    FacilityEvolutionDebugScenarios
                        .CausalFacilityEvolutionSecondActionId
                };
                if (facilityEvolutionActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        facilityEvolutionActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = facilityEvolutionActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             FacilityEvolutionDebugScenarios
                                 .CreateCausalEvolutionFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             FacilityEvolutionDebugScenarios
                                                 .CausalFacilityEvolutionOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] facilitySynthesisActions =
                {
                    FacilitySynthesisDebugScenarios
                        .CausalSynthesisFirstActionId,
                    FacilitySynthesisDebugScenarios
                        .CausalSynthesisSecondActionId
                };
                if (facilitySynthesisActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        facilitySynthesisActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = facilitySynthesisActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             FacilitySynthesisDebugScenarios
                                 .CreateCausalSynthesisFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             FacilitySynthesisDebugScenarios
                                                 .CausalSynthesisOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] infrastructurePowerActions =
                {
                    InfrastructureCommandCausalProductionEntryDebugScenarios
                        .DisconnectActionId,
                    InfrastructureCommandCausalProductionEntryDebugScenarios
                        .ReconnectActionId
                };
                if (infrastructurePowerActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        infrastructurePowerActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = infrastructurePowerActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             InfrastructureCommandCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             InfrastructureCommandCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
                string[] dungeonSpaceActions =
                {
                    DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                        .QuarryActionId,
                    DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                        .StonecuttingActionId
                };
                if (dungeonSpaceActions.Any(actionIds.Contains))
                {
                    string ownerId = OwnerIdForFamily(
                        dungeonSpaceActions,
                        actionIds,
                        campaignBound);
                    string selectedActionId = dungeonSpaceActions.First(
                        actionIds.Contains);
                    foreach (IProductionCausalActionFixture fixture in
                             DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                                 .CreateFixtures(
                                     runId,
                                     ownerId,
                                     ownerId == null
                                         ? null
                                         : worldCheckpoint.Find(
                                             selectedActionId,
                                             DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                                                 .CausalOwnerRestoreKind,
                                             ownerId)))
                    {
                        Add(fixture);
                    }
                }
            }
            catch
            {
                foreach (IProductionCausalActionFixture fixture in fixtures.Values)
                    fixture.Dispose();
                Session.Dispose();
                throw;
            }
        }

        internal LongHorizonNarrativeExecutionExporter.LongHorizonSession Session
        {
            get;
        }

        internal IReadOnlyCollection<IProductionCausalActionFixture> Fixtures =>
            fixtures.Values.OrderBy(value => value.ActionId, StringComparer.Ordinal)
                .ToArray();

        internal JObject CaptureSessionState() => Session.CaptureState();

        internal JObject CaptureWorldCheckpoint() => worldCheckpoint.Capture();

        internal JObject RestoredInputWorldCheckpoint =>
            (JObject)worldCheckpoint.InputCheckpoint.DeepClone();

        internal int RestoredWorldOwnerCount => worldCheckpoint.RestoredOwnerCount;
        internal int IntroducedWorldOwnerCount => worldCheckpoint.IntroducedOwnerCount;
        internal int WorldOwnerCount => worldCheckpoint.OwnerCount;

        internal bool IsSessionOutcome(string outcomeId) =>
            Session.HasExactOutcome(outcomeId);

        internal LongHorizonNarrativeExecutionExporter.LongHorizonCheckpoint
            CaptureSessionCheckpoint(IEnumerable<string> outcomeIds) =>
                Session.CaptureCheckpoint(outcomeIds);

        public JObject CaptureObservedState()
        {
            JObject state = Session.CaptureState();
            var stateOwners = new Dictionary<string, object>(StringComparer.Ordinal);
            var anonymousFixtureStateKeys = new HashSet<string>(StringComparer.Ordinal);
            var sessionStateKeys = new HashSet<string>(
                state.Properties().Select(property => property.Name),
                StringComparer.Ordinal);
            foreach (IProductionCausalActionFixture fixture in fixtures.Values)
            {
                foreach (KeyValuePair<string, object> pair in fixture.CaptureState())
                {
                    JToken value = JToken.FromObject(pair.Value);
                    object runtimeIdentity = fixture is
                            IProductionCausalOwnerCheckpointFixture ownerFixture
                            && ownerFixture.HasPersistentOwner
                        ? ownerFixture.OwnerRuntimeIdentity
                        : null;
                    if (state.TryGetValue(pair.Key, out JToken existing))
                    {
                        bool sameSharedAuthority = runtimeIdentity != null
                            && stateOwners.TryGetValue(
                                pair.Key,
                                out object existingRuntimeIdentity)
                            && ReferenceEquals(
                                runtimeIdentity,
                                existingRuntimeIdentity);
                        // Standalone plan fixtures intentionally omit persistence IDs,
                        // so sibling actions cannot expose their shared Context identity.
                        // Treat equal anonymous views as a read quorum, but never merge a
                        // session key, a persistent owner, or divergent values.
                        bool sameAnonymousView = runtimeIdentity == null
                            && anonymousFixtureStateKeys.Contains(pair.Key)
                            && !sessionStateKeys.Contains(pair.Key)
                            && !stateOwners.ContainsKey(pair.Key);
                        if ((sameSharedAuthority || sameAnonymousView)
                            && JToken.DeepEquals(existing, value))
                            continue;
                        throw new InvalidOperationException(
                            "Duplicate causal observed state key: " + pair.Key);
                    }
                    state[pair.Key] = value;
                    if (runtimeIdentity != null)
                        stateOwners.Add(pair.Key, runtimeIdentity);
                    else
                        anonymousFixtureStateKeys.Add(pair.Key);
                }
            }
            return state;
        }

        public bool HasExactOutcome(string outcomeId) =>
            Session.HasExactOutcome(outcomeId)
            || verifiedFixtureOutcomes.Contains(outcomeId ?? string.Empty);

        internal void RegisterVerifiedFixtureOutcome(
            GameplayOutcomeReplayIdentity identity)
        {
            string outcomeId = identity.OutcomeId.ToString();
            if (outcomeId.Length == 0 || !verifiedFixtureOutcomes.Add(outcomeId))
                throw new InvalidOperationException(
                    "Fixture outcome identity was empty or duplicated: " + outcomeId);
        }

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures.Values)
                fixture.Dispose();
            fixtures.Clear();
            Session.Dispose();
        }

        private void Add(IProductionCausalActionFixture fixture)
        {
            if (fixture == null || !fixtures.TryAdd(fixture.ActionId, fixture))
            {
                fixture?.Dispose();
                throw new InvalidOperationException(
                    "Duplicate or missing production causal fixture.");
            }
            if (fixture is IProductionCausalOwnerCheckpointFixture persistent
                && persistent.HasPersistentOwner)
            {
                worldCheckpoint.Bind(persistent);
            }
        }

        private string OwnerIdFor(string actionId, bool campaignBound)
        {
            if (!campaignBound)
                return null;
            if (!fixtureOwnerIds.TryGetValue(actionId, out string ownerId)
                || string.IsNullOrWhiteSpace(ownerId))
            {
                throw new InvalidOperationException(
                    "Campaign episode lacks an owner binding for "
                    + actionId);
            }
            return CausalGameplaySyntax.RequireId(
                ownerId,
                "campaign fixture owner ID");
        }

        private string OwnerIdForFamily(
            IEnumerable<string> familyActionIds,
            ISet<string> selectedActionIds,
            bool campaignBound)
        {
            if (!campaignBound)
                return null;
            string[] selected = familyActionIds
                .Where(selectedActionIds.Contains)
                .ToArray();
            Require(selected.Length > 0,
                "Campaign owner family has no selected action.");
            string[] ownerIds = selected
                .Select(actionId => OwnerIdFor(actionId, true))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Require(ownerIds.Length == 1,
                "Actions in one campaign owner family must bind the same owner ID.");
            return ownerIds[0];
        }
    }

    private abstract class ProductionActionHandler : ICausalGameplayActionHandler
    {
        protected ProductionActionHandler(
            ProductionPlanWorld world,
            string actionId,
            string domainId,
            MigratedProducerOutcomeKind kind)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            ActionId = actionId;
            DomainId = domainId;
            Kind = kind;
        }

        protected ProductionPlanWorld World { get; }
        protected MigratedProducerOutcomeKind Kind { get; }
        public string ActionId { get; }
        public string DomainId { get; }
        public IReadOnlyCollection<string> ArgumentNames => Array.Empty<string>();

        public CausalGameplayActionResult Execute(
            CausalGameplayActionContext context)
        {
            if (context.Node.Arguments.Count != 0)
                return CausalGameplayActionResult.Rejected(
                    "unexpected-arguments",
                    "This action accepts no arguments.");
            if (!Invoke())
                return CausalGameplayActionResult.Rejected(
                    "production-entry-rejected",
                    "The gameplay owner rejected the action.");
            GameplayOutcomeReplayIdentity identity =
                World.Session.RequireOutcome(Kind);
            JObject evidence = World.Session.CaptureOutcomeEvidence(identity);
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(Kind);
            return new CausalGameplayActionResult(
                true,
                identity.OutcomeId.ToString(),
                definition.OutcomeTypeId.Value,
                string.Empty,
                string.Empty,
                evidence);
        }

        protected abstract bool Invoke();
    }

    private sealed class DrinkCleanWaterHandler : ProductionActionHandler
    {
        internal DrinkCleanWaterHandler(ProductionPlanWorld world)
            : base(
                world,
                "action:drink-clean-water",
                "survival",
                MigratedProducerOutcomeKind.CharacterWaterConsumedEvent) { }

        protected override bool Invoke() => World.Session.ConsumeCleanWater();
    }

    private sealed class RestHandler : ProductionActionHandler
    {
        internal RestHandler(ProductionPlanWorld world)
            : base(
                world,
                "action:rest",
                "life",
                MigratedProducerOutcomeKind.RestOutcomeIdentityEvent) { }

        protected override bool Invoke()
        {
            World.Session.Rest();
            return true;
        }
    }

    private sealed class TrainProficiencyHandler : ProductionActionHandler
    {
        internal TrainProficiencyHandler(ProductionPlanWorld world)
            : base(
                world,
                "action:train-proficiency",
                "career",
                MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt) { }

        protected override bool Invoke()
        {
            World.Session.Train();
            return true;
        }
    }

    private sealed class FixtureActionHandler : ICausalGameplayActionHandler
    {
        private readonly ProductionPlanWorld world;
        private readonly IProductionCausalActionFixture fixture;

        internal FixtureActionHandler(
            ProductionPlanWorld world,
            IProductionCausalActionFixture fixture)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        public string ActionId => fixture.ActionId;
        public string DomainId => fixture.DomainId;
        public IReadOnlyCollection<string> ArgumentNames => Array.Empty<string>();

        internal void ValidateCatalogAction(JObject action, int schemaVersion)
        {
            Require((string)action["outcomeTypeId"] == fixture.OutcomeTypeId.Value,
                "Pinned catalog primary type differs from production fixture.");
            if (schemaVersion < 3) return; // Legacy catalogs did not declare companions.
            JArray declarations = action["companionOutcomes"] as JArray ?? new JArray();
            Require(declarations.Count == fixture.CompanionOutcomeTypeIds.Count,
                "Pinned catalog companion count differs from production fixture.");
            for (int index = 0; index < declarations.Count; index++)
            {
                string relation = schemaVersion >= 4 ? (string)declarations[index]["relation"] : "atomic-sibling";
                Require((string)declarations[index]["outcomeTypeId"] == fixture.CompanionOutcomeTypeIds[index].Value
                    && relation == fixture.CompanionOutcomeRelations[index],
                    "Pinned catalog companion type/relation differs from production fixture.");
            }
        }

        public CausalGameplayActionResult Execute(CausalGameplayActionContext context)
        {
            if (context.Node.Arguments.Count != 0)
                return CausalGameplayActionResult.Rejected(
                    "unexpected-arguments",
                    "This controlled production action accepts no arguments.");
            if (!fixture.Execute())
                return CausalGameplayActionResult.Rejected(
                    "production-entry-rejected",
                    "The gameplay owner rejected the action.");
            GameplayOutcomeReplayIdentity identity = fixture.RequireExactOutcome();
            JObject evidence = fixture.CaptureOutcomeEvidence(identity);
            world.RegisterVerifiedFixtureOutcome(identity);
            IReadOnlyList<GameplayOutcomeReplayIdentity> companionIdentities =
                fixture.RequireExactCompanionOutcomes();
            var companionEvidence = new JArray();
            var companionRelations = new JArray();
            for (int index = 0; index < companionIdentities.Count; index++)
            {
                GameplayOutcomeReplayIdentity companionIdentity =
                    companionIdentities[index];
                JObject companion = fixture.CaptureOutcomeEvidence(
                    companionIdentity);
                string actualType = (string)companion["snapshot"]?["outcomeTypeId"];
                string expectedType = fixture.CompanionOutcomeTypeIds[index].Value;
                if (!string.Equals(actualType, expectedType, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Causal production fixture companion outcome type changed: expected="
                        + expectedType
                        + "; actual="
                        + actualType);
                }
                world.RegisterVerifiedFixtureOutcome(companionIdentity);
                companionEvidence.Add(companion);
                companionRelations.Add(new JObject
                {
                    ["outcomeId"] = companionIdentity.OutcomeId.ToString(),
                    ["relation"] = fixture.CompanionOutcomeRelations[index],
                    ["relatedOutcomeId"] = identity.OutcomeId.ToString()
                });
            }
            return new CausalGameplayActionResult(
                true,
                identity.OutcomeId.ToString(),
                fixture.OutcomeTypeId.Value,
                string.Empty,
                string.Empty,
                evidence,
                companionEvidence,
                companionRelations);
        }
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
    {
        if (value == null)
            throw new InvalidOperationException(label + " must be an object.");
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] wanted = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
        }
    }

    private static string[] RequireStringArray(JToken token, string label)
    {
        JArray values = token as JArray
            ?? throw new InvalidOperationException(label + " must be an array.");
        string[] result = values.Select(value => (string)value).ToArray();
        Require(result.All(value => !string.IsNullOrWhiteSpace(value)),
            label + " contains an empty value.");
        return result;
    }

    private static string[] RequireStringArrayAllowEmpty(
        JToken token,
        string label)
    {
        JArray values = token as JArray
            ?? throw new InvalidOperationException(label + " must be an array.");
        string[] result = values.Select(value => (string)value).ToArray();
        Require(result.All(value => !string.IsNullOrWhiteSpace(value)),
            label + " contains an empty value.");
        return result;
    }

    private static JObject StrictLoad(string path)
    {
        using var reader = new JsonTextReader(File.OpenText(path))
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double
        };
        JObject result = JObject.Load(reader, new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            CommentHandling = CommentHandling.Ignore,
            LineInfoHandling = LineInfoHandling.Load
        });
        if (reader.Read())
            throw new InvalidOperationException("Trailing JSON content is not allowed: " + path);
        return result;
    }

    private static void WriteDelivery(string directory, params string[] files)
    {
        WriteJson(
            Path.Combine(directory, "delivery_manifest.json"),
            new JObject
            {
                ["schemaVersion"] = 1,
                ["files"] = new JArray(files.Select(name => new JObject
                {
                    ["path"] = name,
                    ["sha256"] = Sha256File(Path.Combine(directory, name))
                }))
            });
    }

    private static IEnumerable<JObject> SourceFiles(string projectRoot)
    {
        foreach (string relative in new[]
        {
            "Assets/Scripts/Services/Narrative/CausalExecution/CausalGameplayPlanExecution.cs",
            "Assets/Scripts/Services/Infrastructure/Editor/LiveNarrativeCausalPlan.cs",
            "Assets/Scripts/Services/Infrastructure/Editor/DungeonLiveNarrativeCampaignRunner.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/ProductionCausalPlanBatchExporter.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/ProductionCausalCommercialBatchExporter.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/ProductionCausalCheckpointBatchExporter.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/ProductionCausalActionFixture.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/ProductionCausalOwnerFamilyDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/LongHorizonNarrativeExecutionExporter.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MigratedProducerCombatDefenseEntryE2EDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MigratedProducerRunOffenseEntryE2EDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MigratedProducerCharacterSurvivalEntryE2EDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MigratedProducerEconomyEntryExtendedE2EDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/Editor/EnvironmentCropProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/Editor/CertifiedSeedCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/CertifiedSeedRuntime.cs",
            "Assets/Scripts/Services/Economy/CertifiedSeedSaveSection.cs",
            "Assets/Scripts/Models/Economy/Core/CertifiedSeedModels.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/Editor/EnvironmentFirePhysicalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/Editor/RoomEnvironmentCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentExperienceAdapter.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/EnvironmentRoomOutcomePersistence.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/Editor/SpeciesIncidentCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Character/SpeciesRuntime.cs",
            "Assets/Scripts/Services/Character/SpeciesRuntimeSaveSection.cs",
            "Assets/Scripts/Models/Species/Core/CharacterSpeciesRuntimeContracts.cs",
            "Assets/Scripts/Services/Character/AI/Editor/StaffRebellionResponseCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Character/Work/StaffDiscontentRuntime.cs",
            "Assets/Scripts/Services/Character/Work/StaffDiscontentSaveSection.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/StaffDiscontentOutcomeDefinition.cs",
            "Assets/Scripts/Models/Work/DungeonStaffDiscontentSaveData.cs",
            "Assets/Scripts/Services/Character/Work/StaffDiscontentSystem.cs",
            "Assets/Scripts/Services/Character/Work/StaffDiscontentFacts.cs",
            "Assets/Scripts/Services/Character/Work/StaffDiscontentPublicationRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/StaffDiscontentFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/StaffDiscontentFactPreflight.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/StaffDiscontentPresentationObserver.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/StaffDiscontentTransitionOutcomeDefinition.cs",
            "Assets/Scripts/Services/Character/AI/Editor/StaffDiscontentDebugScenarios.cs",
            "Assets/Scripts/Services/Character/AI/Editor/StaffRebellionResponseDebugScenarios.cs",
            "Assets/Scripts/Services/Character/AI/Editor/CharacterAiEditorTestDependencies.cs",
            "Assets/Scripts/Services/Character/Work/Editor/EmergencyWorkSuspensionCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Character/Work/SettlementAlertRuntime.cs",
            "Assets/Scripts/Services/Character/Work/SettlementAlertContracts.cs",
            "Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs",
            "Assets/Scripts/Services/Character/AI/CharacterAlarmResponseRuntime.cs",
            "Assets/Scripts/Models/Operation/Core/EventAlertSaveContracts.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/EmergencyWorkSuspensionOutcomeDefinition.cs",
            "Assets/Scripts/Services/Offense/Editor/MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Offense/MemoryErasureSealBossAwardService.cs",
            "Assets/Scripts/Services/Offense/OffenseRegionRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/EvolutionOutcomeReceipts.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/EvolutionOutcomeAdapters.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/EvolutionGameplayOutcomeBridge.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/TradeInventory/Editor/TradeInventoryCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Items/PhysicalItemRelocationService.cs",
            "Assets/Scripts/Services/Items/PhysicalItemBatchDispositionService.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/TradeInventory/TradeInventoryOutcomeReceipts.cs",
            "Assets/Scripts/Services/Items/PhysicalItemRelocatedFact.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/TradeInventory/PhysicalItemRelocationFactObservation.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/TradeInventory/PhysicalItemRelocationFactPreflight.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/TradeInventory/PhysicalItemFactDisplayName.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/TradeInventory/TradeInventoryOutcomeRegistration.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/TradeInventory/TradeInventoryOutcomeDefinitions.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/TradeInventory/TradeInventoryPhysicalDispositionOutcomeParticipant.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/TradeInventory/WastePolicyFactObservation.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/TradeInventory/WastePolicyFactPreflight.cs",
            "Assets/Scripts/Models/Economy/Content/WastePolicyChangedFact.cs",
            "Assets/Scripts/Models/Economy/Content/WasteProcessingRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/Editor/DiseaseRouteExposureCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Wildlife/WildlifeDiseaseVectorRuntime.cs",
            "Assets/Scripts/Services/Character/PopulationHealthRuntime.cs",
            "Assets/Scripts/Services/Offense/Editor/OffenseNodeResolutionCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Offense/Editor/OffenseEditorTestDependencies.cs",
            "Assets/Scripts/Services/Offense/OffenseExpeditionRuntime.cs",
            "Assets/Scripts/Services/Offense/OffenseExpeditionModel.cs",
            "Assets/Scripts/Services/Offense/OffenseJourneyModel.cs",
            "Assets/Scripts/Services/Offense/OffenseBattleRuntime.cs",
            "Assets/Scripts/Services/Offense/OffenseExpeditionExperienceRules.cs",
            "Assets/Scripts/Services/Offense/OffenseWorldMapService.cs",
            "Assets/Scripts/Services/Offense/OffenseCampaignCatalogSO.cs",
            "Assets/Resources/SO/Content/OffenseCampaignCatalog.asset",
            "Assets/Scripts/Services/Infrastructure/OffenseSaveService.cs",
            "Assets/Scripts/Services/Character/Core/CharacterProgression.cs",
            "Assets/Scripts/Models/Characters/CharacterProgressionRules.cs",
            "Assets/Scripts/Services/Character/SO/CharacterSO.cs",
            "Assets/Resources/SO/Character/Customer_Orc.asset",
            "Assets/Resources/SO/Character/Species/Species_Orc.asset",
            "Assets/Scripts/Services/Offense/Editor/OffenseTruthRevealCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Offense/OffenseExpeditionResultFinalizer.cs",
            "Assets/Scripts/Services/Offense/OffenseWorldMapRuntime.cs",
            "Assets/Scripts/Services/Offense/OffenseCampaignRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ExternalFaction/ExternalFactionOutcomeReceipts.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ExternalFaction/ExternalFactionOutcomeAdapters.cs",
            "Assets/Scripts/Services/Offense/OffenseTruthRevealedFact.cs",
            "Assets/Scripts/Services/Offense/OffenseExpeditionResolvedFact.cs",
            "Assets/Scripts/Services/Offense/OffenseArrivalSettledFact.cs",
            "Assets/Scripts/Services/Offense/OffenseNodeResolutionFact.cs",
            "Assets/Scripts/Services/Offense/OffenseNodePublicationRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseNodeFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseNodeFactObserver.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseNodeExperienceObserver.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseNodeFactPreflight.cs",
            "Assets/Scripts/Services/Offense/OffenseBattleExperienceFact.cs",
            "Assets/Scripts/Services/Offense/OffenseExpeditionActivityFact.cs",
            "Assets/Scripts/Services/Offense/OffenseExpeditionBattleCompletionHandler.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseBattleExperienceObserver.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseExpeditionActivityObserver.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillActivationFact.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/CharacterSkillActivationFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/CharacterSkillActivationOutcomeDefinition.cs",
            "Assets/Scripts/Services/Offense/OffenseArrivalSettlementRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseArrivalSettlementFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseArrivalSettlementFactObserver.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseArrivalPresentationObserver.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseArrivalSettlementFactPreflight.cs",
            "Assets/Scripts/Services/Offense/OffenseExpeditionResultPublicationRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseExpeditionResultFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseExpeditionResultFactObserver.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseExpeditionResultPresentationObserver.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseExpeditionResultFactPreflight.cs",
            "Assets/Scripts/Services/Offense/OffenseAggregateSaveValidation.cs",
            "Assets/Scripts/Services/Offense/OffenseSaveSections.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ExternalFaction/OffenseTruthRevealFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ExternalFaction/OffenseTruthFactPreflight.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ExternalFaction/OffenseTruthPresentationObserver.cs",
            "Assets/Scripts/Services/Character/AI/Editor/WorkCompletionIdentityCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Character/Identity/Runtime/CharacterIdentityRuntime.cs",
            "Assets/Scripts/Services/Character/Identity/Runtime/CharacterIdentityDomainAdapters.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/WorkCompletionIdentityOutcomeDefinition.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/RunVariableCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Run/RunVariableSystem.cs",
            "Assets/Scripts/Models/CoreSession/RunVariableContracts.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/OwnerRunCompletionCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/VisitorReceptionCheckoutCausalTestPorts.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/TabooButcheryCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/TabooButcheryCausalTestPorts.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/OffenseArrivalCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/FestivalCelebrationCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Character/FestivalExecutionRuntime.cs",
            "Assets/Scripts/Services/Infrastructure/Save/FestivalExecutionSaveSection.cs",
            "Assets/Resources/SO/V20/Society/Festivals/festival_long-night-memorial.asset",
            "Assets/Resources/SO/Building/ResearchOverhaul/RF87_추모실.asset",
            "Assets/Scripts/Services/Offense/OffenseReturnArrivalRuntime.cs",
            "Assets/Scripts/Services/Offense/OffenseReturnArrivalRuntimeServices.cs",
            "Assets/Scripts/Services/Offense/OffenseReturnArrivalAggregateState.cs",
            "Assets/Scripts/Services/Offense/OffenseRewardRuntime.cs",
            "Assets/Scripts/Services/Offense/OffenseRewardSystem.cs",
            "Assets/Scripts/Services/Wildlife/WildlifeActor.cs",
            "Assets/Scripts/Services/Wildlife/WildlifeModels.cs",
            "Assets/Scripts/Services/Combat/CharacterBodyHealthRuntime.cs",
            "Assets/Scripts/Services/Survival/CharacterDeprivationRuntime.cs",
            "Assets/Scripts/Services/Survival/CharacterDeprivationConsequences.cs",
            "Assets/Scripts/Services/Wildlife/WildlifeCarcassService.cs",
            "Assets/Resources/SO/Character/Owners/Owner_Orc.asset",
            "Assets/Resources/SO/Character/Staff/Staff_Generic.asset",
            "Assets/Scripts/Services/Character/AI/Editor/CustomerAiDebugScenarios.cs",
            "Assets/Scripts/Services/Character/Core/CharacterActor.cs",
            "Assets/Scripts/Services/Character/Core/CharacterStats.cs",
            "Assets/Scripts/Services/Character/Core/CharacterActorCollaborators.cs",
            "Assets/Scripts/Services/Character/Core/CharacterBuildingVisitorPort.cs",
            "Assets/Scripts/Models/Buildings/Core/BuildingVisitorPorts.cs",
            "Assets/Scripts/Services/Buildings/ShopCrimeRuntime.cs",
            "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorIncidentHandlers.cs",
            "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs",
            "Assets/Scripts/Services/Infrastructure/CharacterV18RestoreIdentityResolver.cs",
            "Assets/Scripts/Services/Buildings/BuildingStateModule.cs",
            "Assets/Scripts/Services/Buildings/ShopCustomerInteractionService.cs",
            "Assets/Scripts/Services/Buildings/FacilityCrimeRiskUtility.cs",
            "Assets/Scripts/Models/Buildings/Core/ShopServiceCompletion.cs",
            "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs",
            "Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntimeServices.cs",
            "Assets/Resources/SO/Building/Modular/S01_판매카운터.asset",
            "Assets/Scripts/Services/Character/Core/OwnerRunManager.cs",
            "Assets/Scripts/Services/Run/DungeonRunFlowRuntime.cs",
            "Assets/Scripts/Services/Run/RunFlowSaveSection.cs",
            "Assets/Scripts/Services/Meta/MetaRuntimeApplicationAdapter.cs",
            "Assets/Scripts/Models/CoreSession/CoreSessionContracts.cs",
            "Assets/Scripts/Models/Meta/Core/MetaProgressionEffects.cs",
            "Assets/Resources/SO/Character/Owners/Owner_Slime.asset",
            "Assets/Resources/SO/Character/Species/Species_Slime.asset",
            "Assets/Resources/SO/Content/GameDomainContentCatalog.asset",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MetaProgressionCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Infrastructure/Core/MetaProgressionRuntime.cs",
            "Assets/Scripts/Services/Infrastructure/Core/MetaProgressionDomainFacts.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/MetaProgressionFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/MigratedDomainFactObserver.cs",
            "Assets/Scripts/Services/Foundation/Events/DomainFactContracts.cs",
            "Assets/Scripts/Services/Foundation/Events/DomainFactDispatcher.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayDomainFactObservation.cs",
            "Assets/Scripts/Services/Infrastructure/Core/MetaProgressionSaveSection.cs",
            "Assets/Scripts/Models/Meta/Core/MetaProgressionModel.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/RegularCustomerCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Recruitment/RegularCustomerRuntime.cs",
            "Assets/Scripts/Services/Recruitment/RegularCustomerSystem.cs",
            "Assets/Scripts/Services/Recruitment/RegularCustomerSaveSection.cs",
            "Assets/Scripts/Services/Infrastructure/Core/Save/RegularCustomerSaveSection.cs",
            "Assets/Scripts/Models/Recruitment/Core/DungeonRegularCustomerSaveData.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/CharacterLifeCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Character/CharacterLifeApplicationAdapter.cs",
            "Assets/Scripts/Models/Species/Core/CharacterLifeDomain.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/FactionRouteCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/ConsumablesProgressionCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs",
            "Assets/Scripts/Models/Survival/Core/CharacterConsumablesPersistenceContracts.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MealShortageCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Survival/SurvivalFoodRuntime.cs",
            "Assets/Scripts/Models/Survival/Core/SurvivalPrimitives.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/PrimitiveSurvivalCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Survival/CharacterPrimitiveSurvivalRunner.cs",
            "Assets/Scripts/Services/Survival/CharacterDeprivationStateStore.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/ServiceHubDayCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/StockSupplyTransformCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/FacilityWishlistProcurementCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/ObservedCareerArcCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Run/ObservedCareerLifeEventReceipts.cs",
            "Assets/Scripts/Services/Character/CareerApplicationAdapter.cs",
            "Assets/Scripts/Services/Character/PopulationSocialRuntime.cs",
            "Assets/Scripts/Services/Run/V20CampaignRuntime.cs",
            "Assets/Scripts/Views/UI/WarehouseFeatureCommandService.cs",
            "Assets/Scripts/Services/Buildings/SO/StockInfo.cs",
            "Assets/Scripts/Services/Items/PhysicalItemTransformService.cs",
            "Assets/Scripts/Services/Operation/OperatingDaySettlement.cs",
            "Assets/Scripts/Services/Operation/OperatingDaySettlementApplicationAdapter.cs",
            "Assets/Scripts/Services/ServiceRooms/ServiceSessionRuntime.cs",
            "Assets/Scripts/Models/CoreSession/ServiceSessionContracts.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/InvasionLifecycleCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Invasion/InvasionDirectorRuntime.cs",
            "Assets/Scripts/Services/Invasion/InvasionDirectorRuntime.Restore.cs",
            "Assets/Scripts/Services/Invasion/InvasionIntruderSystem.cs",
            "Assets/Scripts/Services/Invasion/InvasionIntruderRuntime.Restore.cs",
            "Assets/Scripts/Services/Invasion/InvasionIntruderExecutionCoordinator.cs",
            "Assets/Scripts/Services/Infrastructure/InvasionSaveRuntimeAdapter.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/Editor/AcquiredTraitMemoryCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/Editor/CaptivityCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptivityInteractionOutcomeDefinition.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptivePerformerMilestoneOutcomeDefinition.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptiveRansomOutcomeDefinition.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptiveEscapeOutcomeDefinition.cs",
            "Assets/Scripts/Services/Captivity/CaptivityInteractionOutcomeRuntime.cs",
            "Assets/Scripts/Services/Captivity/CaptivityInteractionRuntime.cs",
            "Assets/Scripts/Models/Captivity/Core/CaptivityInteractionResolvedFact.cs",
            "Assets/Scripts/Models/Captivity/Core/CaptivityTransitionFacts.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptivityTransitionOutcomeDefinition.cs",
            "Assets/Scripts/Services/Captivity/Editor/CaptivityTransitionOutcomeDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptivityTransitionFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptivityTransitionFactPreflight.cs",
            "Assets/Scripts/Services/Captivity/Editor/CaptivityInteractionOutcomeDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptivityInteractionFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptivityInteractionFactPreflight.cs",
            "Assets/Scripts/Services/Captivity/CaptivePerformerMilestoneOutcomeRuntime.cs",
            "Assets/Scripts/Models/Captivity/Core/CaptivePerformerMilestoneUnlockedFact.cs",
            "Assets/Scripts/Models/Captivity/Core/CaptivityStateModels.cs",
            "Assets/Scripts/Models/Captivity/Core/CaptivitySaveValidation.cs",
            "Assets/Scripts/Services/Captivity/CaptivityRuntime.cs",
            "Assets/Scripts/Services/Captivity/CaptivityRuntimeContexts.cs",
            "Assets/Scripts/Services/Captivity/Editor/CaptivePerformerMilestoneOutcomeDebugScenarios.cs",
            "Assets/Scripts/Services/Infrastructure/Core/Save/CaptivitySaveSection.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptivePerformerMilestoneFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptivePerformerMilestoneFactPreflight.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/SocialLifeOutcomeRegistration.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/Editor/SocialLifeProductionEntryExecutionDebugScenarios.cs",
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonSaveRegistration.cs",
            "Assets/Scripts/Services/Captivity/CaptiveRansomOutcomeRuntime.cs",
            "Assets/Scripts/Models/Captivity/Core/CaptiveRansomAcceptedFact.cs",
            "Assets/Scripts/Services/Captivity/CaptivityRansomRuntime.cs",
            "Assets/Scripts/Services/Infrastructure/GameRuntimeServices.cs",
            "Assets/Scripts/Services/Captivity/Editor/CaptiveRansomOutcomeDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptiveRansomFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptiveRansomFactPreflight.cs",
            "Assets/Scripts/Services/Captivity/CaptiveEscapeOutcomeRuntime.cs",
            "Assets/Scripts/Models/Captivity/Core/CaptiveEscapedFact.cs",
            "Assets/Scripts/Services/Captivity/CaptivityEscapeCompletionRuntime.cs",
            "Assets/Scripts/Services/Captivity/CaptivityEscapeRuntime.cs",
            "Assets/Scripts/Services/Captivity/CaptivityDefectionRuntime.cs",
            "Assets/Scripts/Services/Captivity/Editor/CaptiveEscapeOutcomeDebugScenarios.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptiveEscapeFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CaptiveEscapeFactPreflight.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/Editor/EquipmentApparelCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Infrastructure/Editor/KnowledgeResearchCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Infrastructure/Editor/KnowledgeResearchOutcomeDebugScenarios.cs",
            "Assets/Scripts/Services/Infrastructure/Industrial/Editor/InfrastructureCommandCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs",
            "Assets/Scripts/Services/Infrastructure/Industrial/InfrastructureCommandOutcomeRuntime.cs",
            "Assets/Scripts/Services/Infrastructure/Industrial/InfrastructureCommandOutcomeContracts.cs",
            "Assets/Scripts/Services/Infrastructure/Editor/DungeonSpaceExpansionCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Infrastructure/DungeonSpaceExpansionRuntime.cs",
            "Assets/Scripts/Services/Infrastructure/ModularFacilityWorldSaveService.cs",
            "Assets/Scripts/Services/Synthesis/Editor/FacilitySynthesisCausalProductionEntryDebugScenarios.cs",
            "Assets/Scripts/Services/Synthesis/FacilitySynthesisRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/FacilitySynthesis/FacilitySynthesisOutcomeDefinition.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Infrastructure/DungeonSpaceExpansionOutcomeDefinition.cs",
            "Assets/Scripts/Services/Infrastructure/KnowledgeResidueProcessingRuntime.cs",
            "Assets/Scripts/Services/Infrastructure/BlueprintResearchOutcomeTransaction.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/InfrastructureCommandOutcomeDefinition.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ResearchWorkOutcomeDefinition.cs",
            "Assets/Scripts/Services/Infrastructure/Environment/ApparelPhysicalTransaction.cs",
            "Assets/Scripts/Services/Infrastructure/Environment/CharacterApparelRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/EnvironmentOutcomeReceiptFactory.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/EnvironmentOutcomeReceipts.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/EnvironmentOutcomeAdapters.cs",
            "Assets/Scripts/Models/Environment/Core/EnvironmentalFireActionFacts.cs",
            "Assets/Scripts/Models/Combat/Core/CombatCellShotFact.cs",
            "Assets/Scripts/Models/Combat/Core/CombatCellShotJournal.cs",
            "Assets/Scripts/Models/Combat/Core/CombatActionEvidence.cs",
            "Assets/Scripts/Models/Combat/Core/CombatEquipmentCraftCompletionFactData.cs",
            "Assets/Scripts/Models/Combat/Core/CombatEquipmentSaveContracts.cs",
            "Assets/Scripts/Services/Combat/CombatEquipmentCraftCompletedFact.cs",
            "Assets/Scripts/Services/Character/AI/Idle/IdleSocialInteractionCommandRuntime.cs",
            "Assets/Scripts/Services/Character/AI/Idle/IdleSocialInteractionResolvedFact.cs",
            "Assets/Scripts/Services/Character/AI/Idle/IdleBehavior.cs",
            "Assets/Scripts/Services/Character/AI/Idle/IdleSocialInteractionPendingState.cs",
            "Assets/Scripts/Services/Character/AI/Idle/IdleSocialInteractionPendingSaveSection.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/IdleSocialInteractionFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/IdleSocialInteractionFactPreflight.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/IdleSocialInteractionOutcomeDefinition.cs",
            "Assets/Scripts/Services/Combat/CombatEquipmentQualityResolvedFact.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/CombatEquipmentQualityFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/CombatEquipmentQualityFactPreflight.cs",
            "Assets/Scripts/Services/Combat/CombatEquipmentCraftOutputTransaction.cs",
            "Assets/Scripts/Services/Combat/CombatEquipmentCraftingRuntime.cs",
            "Assets/Scripts/Services/Combat/CombatEquipmentCraftMaterialOutbox.cs",
            "Assets/Scripts/Services/Combat/CombatEquipmentRestoreBuilder.cs",
            "Assets/Scripts/Services/Combat/CombatEquipmentOutputRestorePreflight.cs",
            "Assets/Scripts/Services/Combat/CombatEquipmentSaveJson.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/CombatEquipmentCraftCompletionFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/CombatEquipmentCraftCompletionFactObserver.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/CombatEquipmentCraftCompletionFactPreflight.cs",
            "Assets/Scripts/Models/Combat/Core/CombatActionFact.cs",
            "Assets/Scripts/Models/Combat/Core/CombatActionPendingState.cs",
            "Assets/Scripts/Models/Combat/Core/CombatMutationContracts.cs",
            "Assets/Scripts/Services/Combat/CombatActionPublicationRuntime.cs",
            "Assets/Scripts/Services/Combat/CombatActionPendingSaveSection.cs",
            "Assets/Scripts/Services/Combat/CharacterCombatTransactionFacade.cs",
            "Assets/Scripts/Services/Invasion/DefenseCombatExecutor.cs",
            "Assets/Scripts/Services/Captivity/MinionSettlementSocialRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/CombatActionFactObserver.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/CombatActionFactPreflight.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/CombatActionEvidenceMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/CombatActionFactProbeObserver.cs",
            "Assets/Scripts/Models/Combat/Core/CharacterCombatCommandAggregateState.cs",
            "Assets/Scripts/Models/Combat/Core/CharacterCombatCommandModels.cs",
            "Assets/Scripts/Models/Combat/Core/CharacterCombatCommandPersistence.cs",
            "Assets/Scripts/Models/Combat/Core/CharacterCombatCommandSaveValidation.cs",
            "Assets/Scripts/Services/Combat/CharacterCombatCommandRuntime.cs",
            "Assets/Scripts/Services/Combat/CharacterCombatCommandRestoreCoordinator.cs",
            "Assets/Scripts/Services/Combat/CombatCellShotSaveJson.cs",
            "Assets/Scripts/Services/Combat/CombatSaveSections.cs",
            "Assets/Scripts/Services/Combat/CombatCommandResultApplier.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/CombatAttackEvidenceFactIds.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/CombatCellShotFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/CombatCellShotFactPreflight.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/EnvironmentalFireCompletionIdentity.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/EnvironmentalFireActionFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/EnvironmentalFireActionFactPreflight.cs",
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFireResourceFact.cs",
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFireResourceTransaction.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/EnvironmentalFireResourceFactMapping.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/EnvironmentalFireResourceFactPreflight.cs",
            "Assets/Scripts/Models/Environment/Core/EnvironmentalFireResourceRecord.cs",
            "Assets/Scripts/Services/Economy/CropPlotRuntime.cs",
            "Assets/Scripts/Services/Economy/CropEcologyRuntime.cs",
            "Assets/Scripts/Models/Environment/Core/EnvironmentalFireDomain.cs",
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFireRuntime.cs",
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFireApplicationAdapters.cs",
            "Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFireDamageAuthority.cs",
            "Assets/Scripts/Services/Items/WorldItemStackRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/MigratedProducerOutcomeRuntime.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayOutcomeLedger.cs",
            "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayOutcomePersistence.cs",
            "Assets/Scripts/Models/Defense/Core/DefenseDomain.cs",
            "Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs",
            "Assets/Scripts/Models/Factions/Core/FactionModels.cs",
            "Assets/Scripts/Services/Factions/FactionRuntime.cs",
            "Assets/Scripts/Models/Buildings/Core/ShopContracts.cs",
            "Assets/Scripts/Services/Buildings/Shop.cs",
            "Assets/Scripts/Services/Character/Ability/AbilityShopping.cs",
            "Assets/Scripts/Services/Invasion/DefenseCombatPresentation.cs"
        })
        {
            string absolute = Path.Combine(
                projectRoot,
                relative.Replace('/', Path.DirectorySeparatorChar));
            yield return new JObject
            {
                ["path"] = relative,
                ["sha256"] = Sha256File(absolute)
            };
        }
    }

    private static string ProjectRoot() => Path.GetFullPath(Path.Combine(
        Application.dataPath,
        ".."));

    private static string PrepareCreateOnlyDestination(
        string projectRoot,
        string parentRelative,
        string version)
    {
        string parent = Path.Combine(
            projectRoot,
            parentRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, version);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new InvalidOperationException("Export is create-only: " + destination);
        return destination;
    }

    private static string RequireExistingPath(string value, string label)
    {
        string path = value?.Trim() ?? string.Empty;
        if (path.Length == 0)
            throw new ArgumentException("A " + label + " path is required.");
        string absolute = Path.GetFullPath(path);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("The " + label + " file is missing.", absolute);
        return absolute;
    }

    private static string RequireVersion(string value)
    {
        string version = value?.Trim() ?? string.Empty;
        if (version.Length == 0 || version.Length > 96)
            throw new ArgumentException("A bounded export version is required.");
        if (version.Any(current => !(char.IsLetterOrDigit(current)
                || current is '-' or '_' or '.')))
        {
            throw new ArgumentException("Export version contains an unsupported character.");
        }
        return version;
    }

    private static string ReadGit(string projectRoot, string arguments)
    {
        var start = new DiagnosticsProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using DiagnosticsProcess process = DiagnosticsProcess.Start(start)
            ?? throw new InvalidOperationException("Failed to start git.");
        string output = process.StandardOutput.ReadToEnd().Trim();
        string error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("git failed: " + error);
        return output;
    }

    private static void WriteJson(string path, JToken value) =>
        File.WriteAllText(
            path,
            value.ToString(Formatting.Indented) + "\n",
            new UTF8Encoding(false));

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 algorithm = SHA256.Create();
        return string.Concat(algorithm.ComputeHash(stream)
            .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
