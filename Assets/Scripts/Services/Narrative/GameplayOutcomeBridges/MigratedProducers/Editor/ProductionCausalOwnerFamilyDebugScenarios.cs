#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class ProductionCausalPlanBatchExporter
{
    public static void RunOwnerFamilyCheckpointBatch()
    {
        if (!RunOwnerFamilyCheckpointDebugScenarios(true))
        {
            throw new InvalidOperationException(
                "Production causal owner-family checkpoint batch failed.");
        }
    }

    internal static bool RunOwnerFamilyCheckpointDebugScenarios(bool log = false)
    {
        try
        {
            VerifyLegacyActionCheckpointCompatibility();
            VerifyCropOwnerFamilyAcrossEpisodes();
            VerifyEnvironmentalFireOwnerFamilyAcrossEpisodes();
            VerifySocialConflictApologyOwnerFamilyAcrossEpisodes();
            if (log)
            {
                Debug.Log(
                    "Production causal owner-family checkpoint scenarios passed.");
            }
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Production causal owner-family checkpoint scenarios failed: "
                + exception);
            return false;
        }
    }

    private static void VerifyLegacyActionCheckpointCompatibility()
    {
        const string OwnerId = "facility:causal-legacy-restore-check";
        using IProductionCausalActionFixture fixture =
            MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .CreateCausalFacilityFixture(
                    "run:causal-owner-family:legacy",
                    MigratedProducerOutcomeFaultMode.None,
                    OwnerId);
        var world = new ProductionCausalWorldCheckpoint(
            ProductionCausalWorldCheckpoint.Empty());
        world.Bind((IProductionCausalOwnerCheckpointFixture)fixture);
        JObject checkpoint = world.Capture();
        JObject owner = ((JArray)checkpoint["owners"])
            .Cast<JObject>()
            .Single();
        Require((int)owner["schemaVersion"] == 1
                && string.Equals(
                    (string)owner["actionId"],
                    fixture.ActionId,
                    StringComparison.Ordinal),
            "Legacy action-keyed checkpoint changed shape.");
        ProductionCausalWorldCheckpoint.RequireEveryOwnerRestorable(
            checkpoint,
            "run:causal-owner-family:legacy-audit");
    }

    private static void VerifyCropOwnerFamilyAcrossEpisodes()
    {
        const string OwnerId = "crop-plot:causal-campaign-shared";
        const string ProtagonistId = "character:causal-crop-observer";
        const string IrrigationAction =
            "action:environment-crop-irrigation-supplied";
        const string HarvestAction =
            "action:environment-crop-plan-terminal";
        CausalGameplayPlan irrigationPlan = CreateSingleActionPlan(
            "plan:causal-crop-irrigation-episode",
            "node:causal-crop-irrigation",
            IrrigationAction,
            "irrigation");
        CausalGameplayPlan harvestPlan = CreateSingleActionPlan(
            "plan:causal-crop-harvest-episode",
            "node:causal-crop-harvest",
            HarvestAction,
            "harvest");

        JObject worldCheckpoint;
        LongHorizonNarrativeExecutionExporter.LongHorizonCheckpoint
            sessionCheckpoint;
        using (var world = new ProductionPlanWorld(
                   irrigationPlan,
                   null,
                   "run:causal-crop-campaign:0001",
                   ProtagonistId,
                   null,
                   new Dictionary<string, string>(StringComparer.Ordinal)
                   {
                       [IrrigationAction] = OwnerId
                   },
                   null))
        {
            Require(world.IntroducedWorldOwnerCount == 1
                    && world.RestoredWorldOwnerCount == 0
                    && world.WorldOwnerCount == 1,
                "Crop irrigation episode did not introduce exactly one owner family.");
            CausalGameplayExecutionResult execution =
                new CausalGameplayPlanExecutor(CreateRegistry(world), world)
                    .Execute(irrigationPlan);
            RequireCompleted(
                execution,
                IrrigationAction,
                EnvironmentOutcomeIds.CropIrrigationSupplied);
            worldCheckpoint = world.CaptureWorldCheckpoint();
            sessionCheckpoint = world.CaptureSessionCheckpoint(
                Array.Empty<string>());
        }

        JObject firstOwner = ((JArray)worldCheckpoint["owners"])
            .Cast<JObject>()
            .Single();
        Require((int)firstOwner["schemaVersion"] == 2
                && firstOwner["actionId"] == null
                && string.Equals(
                    (string)firstOwner["ownerRestoreKind"],
                    EnvironmentCropProductionEntryDebugScenarios
                        .CausalCropOwnerRestoreKind,
                    StringComparison.Ordinal),
            "Crop owner checkpoint remained action-keyed.");
        RequireLedgerOutcomeCount(firstOwner, 1);
        ProductionCausalWorldCheckpoint.RequireEveryOwnerRestorable(
            worldCheckpoint,
            "run:causal-owner-family:crop-irrigated-audit");

        JObject finalCheckpoint;
        using (var world = new ProductionPlanWorld(
                   harvestPlan,
                   null,
                   "run:causal-crop-campaign:0002",
                   ProtagonistId,
                   sessionCheckpoint,
                   new Dictionary<string, string>(StringComparer.Ordinal)
                   {
                       [HarvestAction] = OwnerId
                   },
                   worldCheckpoint))
        {
            Require(world.IntroducedWorldOwnerCount == 0
                    && world.RestoredWorldOwnerCount == 1
                    && world.WorldOwnerCount == 1
                    && JToken.DeepEquals(
                        worldCheckpoint,
                        world.RestoredInputWorldCheckpoint),
                "Crop harvest episode did not restore the prior owner family exactly.");
            JObject before = world.CaptureObservedState();
            Require((string)before["crop.phase"] == CropPlotPhase.Growing.ToString()
                    && (float)before["crop.sourceWaterRemaining"] == 2f,
                "Crop owner state was not carried across the episode boundary.");
            CausalGameplayExecutionResult execution =
                new CausalGameplayPlanExecutor(CreateRegistry(world), world)
                    .Execute(harvestPlan);
            RequireCompleted(
                execution,
                HarvestAction,
                EnvironmentOutcomeIds.CropPlanTerminal);
            JObject after = world.CaptureObservedState();
            Require((string)after["crop.phase"] == CropPlotPhase.Empty.ToString()
                    && (int)after["crop.harvestItemCount"] == 6,
                "Restored crop owner did not reach the real terminal harvest state.");
            finalCheckpoint = world.CaptureWorldCheckpoint();
        }

        JObject finalOwner = ((JArray)finalCheckpoint["owners"])
            .Cast<JObject>()
            .Single();
        RequireLedgerOutcomeCount(finalOwner, 2);
        ProductionCausalWorldCheckpoint.RequireEveryOwnerRestorable(
            finalCheckpoint,
            "run:causal-owner-family:crop-harvested-audit");

        var registry = new ProductionCausalWorldCheckpoint(finalCheckpoint);
        bool rejected = false;
        try
        {
            _ = registry.Find(
                HarvestAction,
                "owner-family:incompatible",
                OwnerId);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected,
            "Owner-family checkpoint accepted an incompatible restore family.");
    }

    private static void VerifyEnvironmentalFireOwnerFamilyAcrossEpisodes()
    {
        VerifyPlainFireSuppressionAcrossEpisodes();
        VerifyPhysicalFireChainAcrossEpisodes();
    }

    private static void VerifyPlainFireSuppressionAcrossEpisodes()
    {
        const string OwnerId = "environmental-fire:causal-plain-shared";
        const string ProtagonistId = "character:causal-fire-observer";
        const string IgniteAction = "action:environment-fire-ignite";
        const string SuppressAction =
            "action:environment-fire-initial-attack";
        CausalGameplayPlan ignitePlan = CreateSingleActionPlan(
            "plan:causal-fire-plain-ignite",
            "node:causal-fire-plain-ignite",
            IgniteAction,
            "fire-plain-ignite",
            "environment-fire-ignite-episode");
        CausalGameplayPlan suppressPlan = CreateSingleActionPlan(
            "plan:causal-fire-initial-attack",
            "node:causal-fire-initial-attack",
            SuppressAction,
            "fire-initial-attack",
            "environment-fire-initial-attack-episode");

        JObject worldCheckpoint;
        LongHorizonNarrativeExecutionExporter.LongHorizonCheckpoint
            sessionCheckpoint;
        using (var world = new ProductionPlanWorld(
                   ignitePlan,
                   null,
                   "run:causal-fire-plain:0001",
                   ProtagonistId,
                   null,
                   new Dictionary<string, string>(StringComparer.Ordinal)
                   {
                       [IgniteAction] = OwnerId
                   },
                   null))
        {
            CausalGameplayExecutionResult execution =
                new CausalGameplayPlanExecutor(CreateRegistry(world), world)
                    .Execute(ignitePlan);
            RequireCompleted(
                execution,
                IgniteAction,
                EnvironmentOutcomeIds.FireIgnition);
            JObject state = world.CaptureObservedState();
            Require((string)state["fire.phase"] == "Active"
                    && (bool)state["fire.plainIgnited"],
                "Plain fire ignition did not leave one active fire owner.");
            worldCheckpoint = world.CaptureWorldCheckpoint();
            sessionCheckpoint = world.CaptureSessionCheckpoint(
                Array.Empty<string>());
        }
        RequireLedgerOutcomeCount(
            ((JArray)worldCheckpoint["owners"]).Cast<JObject>().Single(),
            1);

        JObject finalCheckpoint;
        using (var world = new ProductionPlanWorld(
                   suppressPlan,
                   null,
                   "run:causal-fire-plain:0002",
                   ProtagonistId,
                   sessionCheckpoint,
                   new Dictionary<string, string>(StringComparer.Ordinal)
                   {
                       [SuppressAction] = OwnerId
                   },
                   worldCheckpoint))
        {
            Require(world.IntroducedWorldOwnerCount == 0
                    && world.RestoredWorldOwnerCount == 1,
                "Initial-attack episode did not restore its fire owner.");
            CausalGameplayExecutionResult execution =
                new CausalGameplayPlanExecutor(CreateRegistry(world), world)
                    .Execute(suppressPlan);
            RequireCompleted(
                execution,
                SuppressAction,
                EnvironmentOutcomeIds.FireSuppression);
            JObject state = world.CaptureObservedState();
            Require((string)state["fire.phase"] == "Ended"
                    && (bool)state["fire.initialAttackApplied"],
                "Restored plain fire was not extinguished by the real suppression entry.");
            finalCheckpoint = world.CaptureWorldCheckpoint();
        }
        JObject owner = ((JArray)finalCheckpoint["owners"])
            .Cast<JObject>()
            .Single();
        RequireLedgerOutcomeCount(owner, 2);
        ProductionCausalWorldCheckpoint.RequireEveryOwnerRestorable(
            finalCheckpoint,
            "run:causal-owner-family:fire-plain-audit");
    }

    private static void VerifyPhysicalFireChainAcrossEpisodes()
    {
        const string OwnerId = "environmental-fire:causal-physical-shared";
        const string ProtagonistId = "character:causal-fire-observer";
        const string FuelAction =
            "action:environment-fire-fuel-ignition";
        const string DamageAction =
            "action:environment-fire-damage-tick";
        const string WaterAction =
            "action:environment-fire-water-suppression";
        CausalGameplayPlan fuelPlan = CreateSingleActionPlan(
            "plan:causal-fire-fuel-ignition",
            "node:causal-fire-fuel-ignition",
            FuelAction,
            "fire-fuel-ignition",
            "environment-fire-fuel-ignition-episode");
        CausalGameplayPlan damagePlan = CreateSingleActionPlan(
            "plan:causal-fire-damage",
            "node:causal-fire-damage",
            DamageAction,
            "fire-damage",
            "environment-fire-damage-episode");
        CausalGameplayPlan waterPlan = CreateSingleActionPlan(
            "plan:causal-fire-water",
            "node:causal-fire-water",
            WaterAction,
            "fire-water",
            "environment-fire-water-suppression-episode");

        JObject worldCheckpoint;
        LongHorizonNarrativeExecutionExporter.LongHorizonCheckpoint
            sessionCheckpoint;
        using (var world = new ProductionPlanWorld(
                   fuelPlan,
                   null,
                   "run:causal-fire-physical:0001",
                   ProtagonistId,
                   null,
                   new Dictionary<string, string>(StringComparer.Ordinal)
                   {
                       [FuelAction] = OwnerId
                   },
                   null))
        {
            CausalGameplayExecutionResult execution =
                new CausalGameplayPlanExecutor(CreateRegistry(world), world)
                    .Execute(fuelPlan);
            RequireCompleted(
                execution,
                FuelAction,
                EnvironmentOutcomeIds.FireFuelLoss);
            JObject state = world.CaptureObservedState();
            Require((string)state["fire.phase"] == "Active"
                    && (bool)state["fire.fuelIgnited"]
                    && (int)state["fire.fuelStackQuantity"] == 0,
                "Physical-fuel ignition did not consume its exact fuel lot.");
            worldCheckpoint = world.CaptureWorldCheckpoint();
            sessionCheckpoint = world.CaptureSessionCheckpoint(
                Array.Empty<string>());
        }
        RequireLedgerOutcomeCount(
            ((JArray)worldCheckpoint["owners"]).Cast<JObject>().Single(),
            2);

        using (var world = new ProductionPlanWorld(
                   damagePlan,
                   null,
                   "run:causal-fire-physical:0002",
                   ProtagonistId,
                   sessionCheckpoint,
                   new Dictionary<string, string>(StringComparer.Ordinal)
                   {
                       [DamageAction] = OwnerId
                   },
                   worldCheckpoint))
        {
            CausalGameplayExecutionResult execution =
                new CausalGameplayPlanExecutor(CreateRegistry(world), world)
                    .Execute(damagePlan);
            RequireCompleted(
                execution,
                DamageAction,
                EnvironmentOutcomeIds.FireDamage);
            JObject state = world.CaptureObservedState();
            Require((bool)state["fire.damageApplied"]
                    && (float)state["fire.totalDamage"] > 0f
                    && (float)state["fire.targetHitPoints"] < 100f,
                "Restored fire did not commit its real damage owner transition.");
            worldCheckpoint = world.CaptureWorldCheckpoint();
            sessionCheckpoint = world.CaptureSessionCheckpoint(
                Array.Empty<string>());
        }
        RequireLedgerOutcomeCount(
            ((JArray)worldCheckpoint["owners"]).Cast<JObject>().Single(),
            3);

        JObject finalCheckpoint;
        using (var world = new ProductionPlanWorld(
                   waterPlan,
                   null,
                   "run:causal-fire-physical:0003",
                   ProtagonistId,
                   sessionCheckpoint,
                   new Dictionary<string, string>(StringComparer.Ordinal)
                   {
                       [WaterAction] = OwnerId
                   },
                   worldCheckpoint))
        {
            CausalGameplayExecutionResult execution =
                new CausalGameplayPlanExecutor(CreateRegistry(world), world)
                    .Execute(waterPlan);
            RequireCompleted(
                execution,
                WaterAction,
                EnvironmentOutcomeIds.FireWaterConsumed);
            JObject state = world.CaptureObservedState();
            Require((bool)state["fire.waterSuppressed"]
                    && (int)state["fire.totalWaterConsumed"] == 1
                    && (int)state["fire.waterStackQuantity"] == 0,
                "Restored damaged fire did not consume physical water exactly once.");
            finalCheckpoint = world.CaptureWorldCheckpoint();
        }
        JObject finalOwner = ((JArray)finalCheckpoint["owners"])
            .Cast<JObject>()
            .Single();
        RequireLedgerOutcomeCount(finalOwner, 5);
        ProductionCausalWorldCheckpoint.RequireEveryOwnerRestorable(
            finalCheckpoint,
            "run:causal-owner-family:fire-physical-audit");
    }

    private static void VerifySocialConflictApologyOwnerFamilyAcrossEpisodes()
    {
        const string OwnerId = "social-relationship:causal-assault-apology";
        const string ProtagonistId = "character:causal-social-observer";
        string conflictAction =
            SocialLifeCausalProductionEntryDebugScenarios
                .AssaultConflictActionId;
        string apologyAction =
            SocialLifeCausalProductionEntryDebugScenarios.ApologyActionId;
        CausalGameplayPlan conflictPlan = CreateSingleActionPlan(
            "plan:causal-social-assault-conflict",
            "node:causal-social-assault-conflict",
            conflictAction,
            "social-assault-conflict",
            "social-assault-conflict-episode");
        CausalGameplayPlan apologyPlan = CreateSingleActionPlan(
            "plan:causal-social-apology",
            "node:causal-social-apology",
            apologyAction,
            "social-apology",
            "social-apology-episode");

        JObject worldCheckpoint;
        LongHorizonNarrativeExecutionExporter.LongHorizonCheckpoint
            sessionCheckpoint;
        using (var world = new ProductionPlanWorld(
                   conflictPlan,
                   null,
                   "run:causal-social-campaign:0001",
                   ProtagonistId,
                   null,
                   new Dictionary<string, string>(StringComparer.Ordinal)
                   {
                       [conflictAction] = OwnerId
                   },
                   null))
        {
            Require(world.IntroducedWorldOwnerCount == 1
                    && world.RestoredWorldOwnerCount == 0,
                "Social conflict episode did not introduce one relationship owner.");
            CausalGameplayExecutionResult execution =
                new CausalGameplayPlanExecutor(CreateRegistry(world), world)
                    .Execute(conflictPlan);
            RequireCompleted(
                execution,
                conflictAction,
                SocialLifeOutcomeIds.ConflictResolved);
            JObject state = world.CaptureObservedState();
            Require((bool)state["social.assaultExecuted"]
                    && !(bool)state["social.apologyExecuted"]
                    && (bool)state["social.canForgive"]
                    && (float)state["social.recipientHealth"] < 100f
                    && (float)state["social.relationshipSentiment"] < 0f
                    && (int)state["social.identityRuleStateCount"] > 0,
                "Real combat assault did not produce restorable relationship conflict state.");
            worldCheckpoint = world.CaptureWorldCheckpoint();
            sessionCheckpoint = world.CaptureSessionCheckpoint(
                Array.Empty<string>());
        }

        JObject firstOwner = ((JArray)worldCheckpoint["owners"])
            .Cast<JObject>()
            .Single();
        Require((int)firstOwner["schemaVersion"] == 2
                && string.Equals(
                    (string)firstOwner["ownerRestoreKind"],
                    SocialLifeCausalProductionEntryDebugScenarios
                        .CausalSocialOwnerRestoreKind,
                    StringComparison.Ordinal),
            "Social relationship checkpoint did not use its owner family.");
        RequireLedgerOutcomeCount(firstOwner, 2);
        ProductionCausalWorldCheckpoint.RequireEveryOwnerRestorable(
            worldCheckpoint,
            "run:causal-owner-family:social-conflict-audit");

        JObject finalCheckpoint;
        using (var world = new ProductionPlanWorld(
                   apologyPlan,
                   null,
                   "run:causal-social-campaign:0002",
                   ProtagonistId,
                   sessionCheckpoint,
                   new Dictionary<string, string>(StringComparer.Ordinal)
                   {
                       [apologyAction] = OwnerId
                   },
                   worldCheckpoint))
        {
            Require(world.IntroducedWorldOwnerCount == 0
                    && world.RestoredWorldOwnerCount == 1
                    && JToken.DeepEquals(
                        worldCheckpoint,
                        world.RestoredInputWorldCheckpoint),
                "Apology episode did not restore the exact conflict owner.");
            JObject before = world.CaptureObservedState();
            float healthBefore = (float)before["social.recipientHealth"];
            float sentimentBefore = (float)before[
                "social.relationshipSentiment"];
            Require((bool)before["social.canForgive"],
                "Restored assault memory was not eligible for the real apology command.");

            CausalGameplayExecutionResult execution =
                new CausalGameplayPlanExecutor(CreateRegistry(world), world)
                    .Execute(apologyPlan);
            RequireCompleted(
                execution,
                apologyAction,
                SocialLifeOutcomeIds.ApologyResolved);
            JObject after = world.CaptureObservedState();
            Require((bool)after["social.assaultExecuted"]
                    && (bool)after["social.apologyExecuted"]
                    && !(bool)after["social.canForgive"]
                    && Mathf.Approximately(
                        (float)after["social.recipientHealth"],
                        healthBefore)
                    && (float)after["social.relationshipSentiment"]
                        > sentimentBefore,
                "Real apology did not clear the exact memory without rewriting combat damage.");

            IProductionCausalActionFixture apologyFixture = world.Fixtures
                .Single(value => string.Equals(
                    value.ActionId,
                    apologyAction,
                    StringComparison.Ordinal));
            JObject beforeDuplicate = world.CaptureWorldCheckpoint();
            Require(!apologyFixture.Execute(),
                "Completed apology was incorrectly executable a second time.");
            Require(JToken.DeepEquals(
                    beforeDuplicate,
                    world.CaptureWorldCheckpoint()),
                "Rejected duplicate apology changed relationship or outcome state.");
            finalCheckpoint = world.CaptureWorldCheckpoint();
        }

        JObject finalOwner = ((JArray)finalCheckpoint["owners"])
            .Cast<JObject>()
            .Single();
        RequireLedgerOutcomeCount(finalOwner, 3);
        ProductionCausalWorldCheckpoint.RequireEveryOwnerRestorable(
            finalCheckpoint,
            "run:causal-owner-family:social-apology-audit");

        var restoredRegistry = new ProductionCausalWorldCheckpoint(
            finalCheckpoint);
        bool incompatibleRejected = false;
        try
        {
            _ = restoredRegistry.Find(
                apologyAction,
                "owner-family:incompatible-social",
                OwnerId);
        }
        catch (InvalidOperationException)
        {
            incompatibleRejected = true;
        }
        Require(incompatibleRejected,
            "Social owner accepted an incompatible restore family.");

        JObject tampered = (JObject)finalCheckpoint.DeepClone();
        JObject tamperedDomain = (JObject)((JArray)tampered["owners"])[0]
            ["domainState"];
        tamperedDomain["recipientId"] =
            "character:causal-social-recipient:tampered";
        bool tamperRejected = false;
        try
        {
            ProductionCausalWorldCheckpoint.RequireEveryOwnerRestorable(
                tampered,
                "run:causal-owner-family:social-tamper-audit");
        }
        catch (InvalidOperationException)
        {
            tamperRejected = true;
        }
        Require(tamperRejected,
            "Social owner accepted tampered participant identity.");
    }

    private static CausalGameplayPlan CreateSingleActionPlan(
        string planId,
        string nodeId,
        string actionId,
        string hashSalt,
        string templateId = "crop-irrigation-growth-harvest")
    {
        string planHash = ProductionCausalOutcomeEvidence.Sha256Text(
            "causal-owner-family-plan|" + hashSalt);
        string catalogHash = ProductionCausalOutcomeEvidence.Sha256Text(
            "causal-actions-v3-owner-family-debug");
        CausalGameplayPlanNode node = new(
            nodeId,
            actionId,
            new JObject(),
            Array.Empty<string>(),
            Array.Empty<CausalGameplayPredicate>(),
            Array.Empty<CausalGameplayPredicate>());
        return new CausalGameplayPlan(
            planId,
            1,
            planHash,
            string.Empty,
            "causal-actions-v3",
            catalogHash,
            new[] { node },
            new[] { nodeId },
            2,
            templateId,
            1);
    }

    private static void RequireCompleted(
        CausalGameplayExecutionResult execution,
        string actionId,
        GameplayOutcomeTypeId outcomeTypeId)
    {
        CausalGameplayNodeRecord record = execution?.Records.SingleOrDefault();
        Require(execution != null
                && execution.IsComplete
                && !execution.ReplanRequired
                && record != null
                && record.Status == CausalGameplayNodeStatus.Completed
                && string.Equals(record.ActionId, actionId, StringComparison.Ordinal)
                && string.Equals(
                    record.OutcomeTypeId,
                    outcomeTypeId.Value,
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(record.OutcomeId)
                && record.OutcomeEvidence != null,
            "Owner-family episode did not complete with its exact outcome: "
            + actionId);
    }

    private static void RequireLedgerOutcomeCount(JObject owner, int expected)
    {
        int count = ((JArray)owner["outcomeLedger"]?["exactOutcomes"]
            ?? throw new InvalidOperationException(
                "Owner-family checkpoint has no exact outcome array.")).Count;
        Require(count == expected,
            "Owner-family outcome ledger count mismatch. expected="
            + expected
            + "; actual="
            + count);
    }
}
#endif
