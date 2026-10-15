#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class EnvironmentFirePhysicalProductionEntryDebugScenarios
{
    internal const string CausalFireOwnerRestoreKind =
        "owner-family:environmental-fire";
    private const string CausalFireOwnerKind =
        "owner-kind:environmental-fire";
    private const string CausalFireDomainStateSchema =
        "dungeonstory-causal-fire-owner-state@1";
    private const string FuelOperationPrefix =
        "environmental-fire-fuel-loss:";
    private const string WaterOperationId =
        "qa:environment-fire-physical:water-suppression";

    public static bool RunAll(bool log = false)
    {
        using Fixture fixture = new();

        string fuelStackId = WorldItemRepositoryEditorAccess.AddStack(
            fixture.Repository,
            "item:buffer",
            2,
            WorldItemStackState.Stored,
            position: fixture.FireWorld.TargetPosition);
        var fuelRequest = new EnvironmentalFireFuelLossRequest(
            new EnvironmentalFireTargetRef(
                EnvironmentalFireTargetKind.ItemStack,
                fuelStackId),
            2);
        string fuelOperationId = FuelOperationPrefix + fixture.CauseId;
        Require(
            fixture.Repository.GetEditorTestQuantity(fuelStackId) == 2
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0
            && fixture.CountExact(
                EnvironmentOutcomeIds.FireFuelProducer,
                EnvironmentOutcomeIds.FireFuelLoss) == 0,
            "The fuel stack must be intact with no physical outbox before the ignition owner runs.");
        int versionBeforeIgnition = fixture.FireRuntime.Version;
        EnvironmentalFireIgnitionResult ignition = fixture.FireRuntime.TryIgnite(
            new EnvironmentalFireIgnitionRequest(
                fixture.CauseId,
                EnvironmentalFireIgnitionKind.FeedSelfHeating,
                "qa:environment-fire-physical",
                fixture.FireWorld.Target,
                0.8f,
                "qa:environment-fire-physical:fuel-evidence",
                fuelRequest,
                fixture.FireWorld.TargetDisplayName));
        EnvironmentalFireSnapshot ignited = fixture.FireRuntime.ActiveFires
            .SingleOrDefault(value => value != null
                && string.Equals(value.FireId, ignition.FireId,
                    StringComparison.Ordinal));
        Require(
            ignition.Created
            && fixture.FireRuntime.Version > versionBeforeIgnition
            && ignited != null
            && ignited.FuelLossTarget.Equals(new EnvironmentalFireTargetRef(
                EnvironmentalFireTargetKind.ItemStack,
                fuelStackId))
            && string.Equals(ignited.FuelLossOperationId, fuelOperationId,
                StringComparison.Ordinal)
            && ignited.FuelLossQuantity == 2
            && ignited.FuelLossMassGrams > 0L
            && ignited.FuelLossAcknowledged
            && fixture.Items.GetAllStacks().All(value => value == null
                || !string.Equals(value.StackId, fuelStackId,
                    StringComparison.Ordinal))
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0,
            "EnvironmentalFireRuntime.TryIgnite did not drive "
            + "EnvironmentalFireWorldAdapter.TryCommitPending through the real "
            + "fuel participant and acknowledged physical Sink. Observed: "
            + "disposition=" + ignition.Disposition
            + "; created=" + ignition.Created
            + "; fireId=" + ignition.FireId
            + "; reason=" + ignition.Reason
            + "; runtimeVersion=" + versionBeforeIgnition + "->"
            + fixture.FireRuntime.Version
            + "; activeFireCount=" + fixture.FireRuntime.ActiveFires.Count
            + "; fuelStackQuantity="
            + fixture.Repository.GetEditorTestQuantity(fuelStackId)
            + "; physicalPendingCount="
            + fixture.Repository.GetEditorPendingBatchDispositionCount()
            + "; fireFuelOutcomeCount=" + fixture.CountExact(
                EnvironmentOutcomeIds.FireFuelProducer,
                EnvironmentOutcomeIds.FireFuelLoss)
            + "; ignitionOutcomeCount=" + fixture.CountExact(
                EnvironmentOutcomeIds.FireIgnitionProducer,
                EnvironmentOutcomeIds.FireIgnition) + ".");
        fixture.RequireAcknowledged(
            EnvironmentOutcomeIds.FireFuelProducer,
            EnvironmentOutcomeIds.FireFuelLoss,
            fuelOperationId,
            GameplayOutcomeStatus.Failed);

        string destinationId = EnvironmentalFireWaterDestinationIdentity.Create(
            ignition.FireId);
        string waterStackId = WorldItemRepositoryEditorAccess.AddStack(
            fixture.Repository,
            EnvironmentalFireWorldAdapter.CleanWaterItemId,
            1,
            WorldItemStackState.FacilityBuffer,
            destinationId,
            position: fixture.FireWorld.TargetPosition);
        WorldItemStackSnapshot waterBefore = fixture.Items.GetAllStacks()
            .Single(value => value != null
                && string.Equals(value.StackId, waterStackId,
                    StringComparison.Ordinal));
        Require(
            fixture.QuantityReservations.TryReserve(
                WaterOperationId,
                fixture.WorkerId.Value,
                ItemReservationPurpose.FacilityBuffer,
                destinationId,
                new ItemQuantityReservationRequest(
                    new ItemStackId(waterStackId),
                    1,
                    waterBefore.ReservationSignature),
                out ItemQuantityLease lease,
                out DomainFailure reserveFailure),
            "The real clean-water quantity lease was not created: "
            + reserveFailure);
        Require(
            lease != null
            && string.Equals(lease.ownerOperationId, WaterOperationId,
                StringComparison.Ordinal)
            && string.Equals(lease.ownerCharacterId, fixture.WorkerId.Value,
                StringComparison.Ordinal)
            && string.Equals(lease.aggregationCohortId, destinationId,
                StringComparison.Ordinal)
            && fixture.QuantityReservations.GetReservedQuantity(
                new ItemStackId(waterStackId)) == 1,
            "The clean-water lease lost its operation, worker, destination, or exact quantity identity.");
        Require(
            fixture.Repository.GetEditorTestQuantity(waterStackId) == 1
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0
            && fixture.CountExact(
                EnvironmentOutcomeIds.FireWaterProducer,
                EnvironmentOutcomeIds.FireWaterConsumed) == 0,
            "The water stack and lease must be intact with no physical outbox before the suppression owner runs.");

        int versionBeforeSuppression = fixture.FireRuntime.Version;
        float intensityBefore = ignited.Intensity;
        EnvironmentalFireSuppressionResult suppression =
            fixture.FireRuntime.TryApplySuppression(
                new EnvironmentalFireSuppressionCommand(
                    WaterOperationId,
                    ignition.FireId,
                    fixture.WorkerId.Value,
                    fixture.FireWorld.StandPosition,
                    EnvironmentalFireSuppressionMode.Water,
                    1f,
                    lease.leaseId,
                    1,
                    fixture.Worker.BuildingDisplayName));
        EnvironmentalFireSnapshot suppressed = fixture.FireRuntime.ActiveFires
            .SingleOrDefault(value => value != null
                && string.Equals(value.FireId, ignition.FireId,
                    StringComparison.Ordinal));
        Require(
            suppression.Applied
            && suppression.Disposition
                == EnvironmentalFireSuppressionDisposition.Applied
            && suppression.WaterConsumed == 1
            && !string.IsNullOrWhiteSpace(suppression.WaterCommitId)
            && Mathf.Approximately(suppression.IntensityBefore, intensityBefore)
            && Mathf.Approximately(
                suppression.IntensityAfter,
                intensityBefore
                    - fixture.FireWorld.Profile.WaterSuppressionPerUnit)
            && fixture.FireRuntime.Version > versionBeforeSuppression
            && suppressed != null
            && Mathf.Approximately(
                suppressed.Intensity,
                suppression.IntensityAfter)
            && suppressed.TotalWaterConsumed == 1
            && fixture.Items.GetAllStacks().All(value => value == null
                || !string.Equals(value.StackId, waterStackId,
                    StringComparison.Ordinal))
            && !fixture.QuantityReservations.Revalidate(
                lease.leaseId,
                out _,
                out _)
            && fixture.QuantityReservations.GetReservedQuantity(
                new ItemStackId(waterStackId)) == 0
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0,
            "EnvironmentalFireRuntime.TryApplySuppression did not drive "
            + "EnvironmentalFireWorldAdapter.TryCommitReservedWaterPending "
            + "through the real water participant and acknowledge the exact "
            + "lease, physical Sink, and fire-state delta.");
        fixture.RequireAcknowledged(
            EnvironmentOutcomeIds.FireWaterProducer,
            EnvironmentOutcomeIds.FireWaterConsumed,
            WaterOperationId,
            GameplayOutcomeStatus.Succeeded);

        GameplayOutcomeSnapshot suppressionOutcome = fixture.Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.producerId,
                    EnvironmentOutcomeIds.FireSuppressionProducer,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.outcomeTypeId,
                    EnvironmentOutcomeIds.FireSuppression.Value,
                    StringComparison.Ordinal));
        string committedSuppressionKey = suppressionOutcome == null
            ? string.Empty
            : new GameplayResultKey(
                suppressionOutcome.producerId,
                new GameplayOperationId(suppressionOutcome.operationId),
                suppressionOutcome.ownerRevision,
                suppressionOutcome.localResultIndex).ToString();

        Require(
            fixture.CountExact(
                EnvironmentOutcomeIds.FireFuelProducer,
                EnvironmentOutcomeIds.FireFuelLoss) == 1
            && fixture.CountExact(
                EnvironmentOutcomeIds.FireWaterProducer,
                EnvironmentOutcomeIds.FireWaterConsumed) == 1
            && fixture.CountExact(
                EnvironmentOutcomeIds.FireSuppressionProducer,
                EnvironmentOutcomeIds.FireSuppression) == 1
            && string.Equals(
                suppression.OutcomeResultKey,
                committedSuppressionKey,
                StringComparison.Ordinal),
            "The shared ledger did not retain exactly one physical fuel, "
            + "physical water, and canonical fire-suppression outcome, or the "
            + "domain result lost its exact suppression result key.");

        if (log)
        {
            Debug.Log(
                "Environment fire physical production-entry outcomes PASS: "
                + EnvironmentOutcomeIds.FireFuelProducer + "/"
                + EnvironmentOutcomeIds.FireFuelLoss.Value + ", "
                + EnvironmentOutcomeIds.FireWaterProducer + "/"
                + EnvironmentOutcomeIds.FireWaterConsumed.Value);
        }
        return true;
    }

    internal static IReadOnlyList<IProductionCausalActionFixture>
        CreateCausalFireFixtures(
            string causalRunId,
            string ownerId = null,
            JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException(
                "A causal run ID is required.",
                nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A fire owner checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new CausalFireFixtureContext(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            5,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            CreateCausalFixture(
                context,
                lifetime,
                "action:environment-fire-ignite",
                EnvironmentOutcomeIds.FireIgnition,
                context.CaptureState,
                context.IgniteWithoutPhysicalFuel,
                context.RequireIgnitionOutcome),
            CreateCausalFixture(
                context,
                lifetime,
                "action:environment-fire-initial-attack",
                EnvironmentOutcomeIds.FireSuppression,
                EmptyState,
                context.ApplyInitialAttack,
                context.RequireSuppressionOutcome),
            CreateCausalFixture(
                context,
                lifetime,
                "action:environment-fire-fuel-ignition",
                EnvironmentOutcomeIds.FireFuelLoss,
                EmptyState,
                context.IgniteWithPhysicalFuel,
                context.RequireFuelOutcome),
            CreateCausalFixture(
                context,
                lifetime,
                "action:environment-fire-damage-tick",
                EnvironmentOutcomeIds.FireDamage,
                EmptyState,
                context.AdvanceDamageTick,
                context.RequireDamageOutcome),
            CreateCausalFixture(
                context,
                lifetime,
                "action:environment-fire-water-suppression",
                EnvironmentOutcomeIds.FireWaterConsumed,
                EmptyState,
                context.ApplyPhysicalWater,
                context.RequireWaterOutcome)
        };
    }

    private static IProductionCausalActionFixture CreateCausalFixture(
        CausalFireFixtureContext context,
        SharedProductionCausalFixtureLifetime lifetime,
        string actionId,
        GameplayOutcomeTypeId outcomeTypeId,
        Func<IReadOnlyDictionary<string, object>> capture,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireExact) =>
        new DelegateProductionCausalActionFixture(
            actionId,
            "environment",
            outcomeTypeId,
            capture,
            execute,
            requireExact,
            context.CaptureOutcomeEvidence,
            lifetime.Release,
            context.OwnerId,
            context.OwnerKind,
            context.HasPersistentOwner
                ? context.CaptureOwnerCheckpoint
                : null,
            context.HasPersistentOwner
                ? CausalFireOwnerRestoreKind
                : null,
            context.HasPersistentOwner ? context : null);

    private static IReadOnlyDictionary<string, object> EmptyState() =>
        new Dictionary<string, object>();

    private sealed class CausalFireFixtureContext : IDisposable
    {
        private const string PlainCauseId =
            "causal:environment-fire:plain-ignition";
        private const string PhysicalCauseId =
            "causal:environment-fire:fuel-ignition";
        private const string InitialAttackOperationId =
            "causal:environment-fire:initial-attack";
        private const string WaterOperationId =
            "causal:environment-fire:water-suppression";

        private readonly Fixture fixture;
        private readonly string ownerId;
        private string fuelStackId = string.Empty;
        private string waterStackId = string.Empty;
        private bool plainIgnited;
        private bool fuelIgnited;
        private bool initialAttackApplied;
        private bool damageApplied;
        private bool waterSuppressed;

        internal CausalFireFixtureContext(
            string causalRunId,
            string ownerId,
            JObject ownerCheckpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    CausalFireOwnerKind)
                : causalRunId + ":environment-fire-ledger";
            fixture = new Fixture(ledgerRunId);
            try
            {
                if (ownerCheckpoint == null)
                    CreateInitialFuelStack();
                else
                    RestoreOwnerCheckpoint(ownerCheckpoint);
            }
            catch
            {
                fixture.Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string OwnerId => ownerId;
        internal string OwnerKind => HasPersistentOwner
            ? CausalFireOwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            EnvironmentalFireSnapshot active = fixture.FireRuntime.ActiveFires
                .SingleOrDefault();
            EnvironmentalFireHistorySnapshot ended = active == null
                ? fixture.FireRuntime.History.LastOrDefault()
                : null;
            return new Dictionary<string, object>
            {
                ["fire.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:environment-fire-ephemeral",
                ["fire.phase"] = active != null
                    ? "Active"
                    : ended != null ? "Ended" : "None",
                ["fire.fireId"] = active?.FireId
                    ?? ended?.Fire?.FireId
                    ?? string.Empty,
                ["fire.intensity"] = active?.Intensity ?? 0f,
                ["fire.remainingFuel"] = active?.RemainingFuel ?? 0f,
                ["fire.totalDamage"] = active?.TotalDamage
                    ?? ended?.Fire?.TotalDamage
                    ?? 0f,
                ["fire.totalWaterConsumed"] = active?.TotalWaterConsumed
                    ?? ended?.Fire?.TotalWaterConsumed
                    ?? 0,
                ["fire.targetHitPoints"] = fixture.FireWorld.HitPoints,
                ["fire.fuelStackQuantity"] = fuelStackId.Length == 0
                    ? 0
                    : fixture.Repository.GetEditorTestQuantity(fuelStackId),
                ["fire.waterStackQuantity"] = waterStackId.Length == 0
                    ? 0
                    : fixture.Repository.GetEditorTestQuantity(waterStackId),
                ["fire.plainIgnited"] = plainIgnited,
                ["fire.fuelIgnited"] = fuelIgnited,
                ["fire.initialAttackApplied"] = initialAttackApplied,
                ["fire.damageApplied"] = damageApplied,
                ["fire.waterSuppressed"] = waterSuppressed
            };
        }

        internal bool IgniteWithoutPhysicalFuel()
        {
            if (HasStarted())
                return false;
            EnvironmentalFireIgnitionResult result = fixture.FireRuntime.TryIgnite(
                new EnvironmentalFireIgnitionRequest(
                    PlainCauseId,
                    EnvironmentalFireIgnitionKind.ProcessAccident,
                    "causal-environment-fire",
                    fixture.FireWorld.Target,
                    0.25f,
                    "evidence:causal-environment-fire:plain",
                    targetDisplayName: fixture.FireWorld.TargetDisplayName));
            plainIgnited = result.Created;
            return plainIgnited;
        }

        internal bool ApplyInitialAttack()
        {
            if (!plainIgnited
                || fuelIgnited
                || initialAttackApplied
                || fixture.FireRuntime.ActiveFires.Count != 1)
            {
                return false;
            }
            EnvironmentalFireSnapshot fire = fixture.FireRuntime.ActiveFires.Single();
            EnvironmentalFireSuppressionResult result =
                fixture.FireRuntime.TryApplySuppression(
                    new EnvironmentalFireSuppressionCommand(
                        InitialAttackOperationId,
                        fire.FireId,
                        fixture.WorkerId.Value,
                        fixture.FireWorld.StandPosition,
                        EnvironmentalFireSuppressionMode.InitialAttack,
                        13f,
                        workerDisplayName: fixture.Worker.Identity.DisplayName));
            initialAttackApplied = result.Disposition
                == EnvironmentalFireSuppressionDisposition.Extinguished;
            return initialAttackApplied;
        }

        internal bool IgniteWithPhysicalFuel()
        {
            if (HasStarted()
                || fuelStackId.Length == 0
                || fixture.Repository.GetEditorTestQuantity(fuelStackId) != 2)
            {
                return false;
            }
            var fuelRequest = new EnvironmentalFireFuelLossRequest(
                new EnvironmentalFireTargetRef(
                    EnvironmentalFireTargetKind.ItemStack,
                    fuelStackId),
                2);
            EnvironmentalFireIgnitionResult result = fixture.FireRuntime.TryIgnite(
                new EnvironmentalFireIgnitionRequest(
                    PhysicalCauseId,
                    EnvironmentalFireIgnitionKind.FeedSelfHeating,
                    "causal-environment-fire",
                    fixture.FireWorld.Target,
                    0.8f,
                    "evidence:causal-environment-fire:fuel",
                    fuelRequest,
                    fixture.FireWorld.TargetDisplayName));
            EnvironmentalFireSnapshot fire = fixture.FireRuntime.ActiveFires
                .SingleOrDefault();
            fuelIgnited = result.Created
                && fire != null
                && fire.FuelLossAcknowledged
                && fixture.Repository.GetEditorTestQuantity(fuelStackId) == 0;
            return fuelIgnited;
        }

        internal bool AdvanceDamageTick()
        {
            if (!fuelIgnited
                || damageApplied
                || waterSuppressed
                || fixture.FireRuntime.ActiveFires.Count != 1)
            {
                return false;
            }
            float hitPointsBefore = fixture.FireWorld.HitPoints;
            EnvironmentalFireTickReport report = fixture.FireRuntime.Advance(5f);
            damageApplied = report.Steps == 1
                && report.DamageCommits == 1
                && report.Failures.Count == 0
                && fixture.FireWorld.HitPoints < hitPointsBefore;
            return damageApplied;
        }

        internal bool ApplyPhysicalWater()
        {
            if (!fuelIgnited
                || !damageApplied
                || waterSuppressed
                || fixture.FireRuntime.ActiveFires.Count != 1)
            {
                return false;
            }
            EnvironmentalFireSnapshot fire = fixture.FireRuntime.ActiveFires.Single();
            string destinationId = EnvironmentalFireWaterDestinationIdentity.Create(
                fire.FireId);
            waterStackId = WorldItemRepositoryEditorAccess.AddStack(
                fixture.Repository,
                EnvironmentalFireWorldAdapter.CleanWaterItemId,
                1,
                WorldItemStackState.FacilityBuffer,
                destinationId,
                position: fixture.FireWorld.TargetPosition);
            WorldItemStackSnapshot water = fixture.Items.GetAllStacks()
                .Single(value => value != null
                    && string.Equals(
                        value.StackId,
                        waterStackId,
                        StringComparison.Ordinal));
            if (!fixture.QuantityReservations.TryReserve(
                    WaterOperationId,
                    fixture.WorkerId.Value,
                    ItemReservationPurpose.FacilityBuffer,
                    destinationId,
                    new ItemQuantityReservationRequest(
                        new ItemStackId(waterStackId),
                        1,
                        water.ReservationSignature),
                    out ItemQuantityLease lease,
                    out _))
            {
                return false;
            }
            EnvironmentalFireSuppressionResult result =
                fixture.FireRuntime.TryApplySuppression(
                    new EnvironmentalFireSuppressionCommand(
                        WaterOperationId,
                        fire.FireId,
                        fixture.WorkerId.Value,
                        fixture.FireWorld.StandPosition,
                        EnvironmentalFireSuppressionMode.Water,
                        1f,
                        lease.leaseId,
                        1,
                        fixture.Worker.Identity.DisplayName));
            waterSuppressed = result.Applied
                && result.WaterConsumed == 1
                && fixture.Repository.GetEditorTestQuantity(waterStackId) == 0;
            return waterSuppressed;
        }

        internal GameplayOutcomeReplayIdentity RequireIgnitionOutcome() =>
            fixture.RequirePublishedIdentity(
                EnvironmentOutcomeIds.FireIgnitionProducer,
                EnvironmentOutcomeIds.FireIgnition);

        internal GameplayOutcomeReplayIdentity RequireSuppressionOutcome() =>
            fixture.RequirePublishedIdentity(
                EnvironmentOutcomeIds.FireSuppressionProducer,
                EnvironmentOutcomeIds.FireSuppression);

        internal GameplayOutcomeReplayIdentity RequireFuelOutcome() =>
            fixture.RequirePublishedIdentity(
                EnvironmentOutcomeIds.FireFuelProducer,
                EnvironmentOutcomeIds.FireFuelLoss);

        internal GameplayOutcomeReplayIdentity RequireDamageOutcome() =>
            fixture.RequirePublishedIdentity(
                EnvironmentOutcomeIds.FireDamageProducer,
                EnvironmentOutcomeIds.FireDamage);

        internal GameplayOutcomeReplayIdentity RequireWaterOutcome() =>
            fixture.RequirePublishedIdentity(
                EnvironmentOutcomeIds.FireWaterProducer,
                EnvironmentOutcomeIds.FireWaterConsumed);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(fixture.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "This causal fire context has no persistent owner.");
            }
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                CausalFireOwnerKind,
                CausalFireOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = CausalFireDomainStateSchema,
                    ["fireRuntime"] = Serialize(
                        fixture.FireRuntime.Capture(),
                        "environmental-fire runtime"),
                    ["damageOutcomes"] = new JArray(
                        fixture.DamageAuthority.Capture().Select(value =>
                            Serialize(value, "environmental-fire damage outcome"))),
                    ["resourceOperations"] = new JArray(
                        fixture.ResourceTransaction.Capture().Select(value =>
                            Serialize(value, "environmental-fire resource operation"))),
                    ["physicalItems"] = Serialize(
                        fixture.Items.Capture(),
                        "physical-item state"),
                    ["targetHitPoints"] = fixture.FireWorld.HitPoints,
                    ["fuelStackId"] = fuelStackId,
                    ["waterStackId"] = waterStackId,
                    ["plainIgnited"] = plainIgnited,
                    ["fuelIgnited"] = fuelIgnited,
                    ["initialAttackApplied"] = initialAttackApplied,
                    ["damageApplied"] = damageApplied,
                    ["waterSuppressed"] = waterSuppressed
                },
                fixture.Ledger);
        }

        public void Dispose() => fixture.Dispose();

        private bool HasStarted() => plainIgnited
            || fuelIgnited
            || fixture.FireRuntime.ActiveFires.Count > 0
            || fixture.FireRuntime.History.Count > 0;

        private void CreateInitialFuelStack()
        {
            fuelStackId = WorldItemRepositoryEditorAccess.AddStack(
                fixture.Repository,
                "material:lumber",
                2,
                WorldItemStackState.Stored,
                position: fixture.FireWorld.TargetPosition);
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                CausalFireOwnerKind,
                CausalFireOwnerRestoreKind,
                fixture.Ledger);
            RequireFields(
                state,
                "causal fire owner state",
                "schemaVersion",
                "schema",
                "fireRuntime",
                "damageOutcomes",
                "resourceOperations",
                "physicalItems",
                "targetHitPoints",
                "fuelStackId",
                "waterStackId",
                "plainIgnited",
                "fuelIgnited",
                "initialAttackApplied",
                "damageApplied",
                "waterSuppressed");
            Require(
                (int)state["schemaVersion"] == 1
                && string.Equals(
                    (string)state["schema"],
                    CausalFireDomainStateSchema,
                    StringComparison.Ordinal),
                "Causal fire owner state header is invalid.");

            fixture.Items.Restore(Deserialize<DungeonPhysicalItemSaveData>(
                state["physicalItems"],
                "physical-item state"));
            fixture.DamageAuthority.Restore(
                ((JArray)state["damageOutcomes"])
                    .Select(value => Deserialize<
                        EnvironmentalFireDamageRecord>(
                            value,
                            "environmental-fire damage outcome"))
                    .ToArray());
            fixture.ResourceTransaction.Restore(
                ((JArray)state["resourceOperations"])
                    .Select(value => Deserialize<
                        EnvironmentalFireResourceRecord>(
                            value,
                            "environmental-fire resource operation"))
                    .ToArray());
            fixture.FireWorld.RestoreHitPoints((float)state["targetHitPoints"]);
            DungeonEnvironmentalFireSaveData fire = Deserialize<
                DungeonEnvironmentalFireSaveData>(
                    state["fireRuntime"],
                    "environmental-fire runtime");
            fixture.FireRuntime.Restore(
                fixture.FireRuntime.PrepareRestore(fire));
            fuelStackId = (string)state["fuelStackId"] ?? string.Empty;
            waterStackId = (string)state["waterStackId"] ?? string.Empty;
            plainIgnited = (bool)state["plainIgnited"];
            fuelIgnited = (bool)state["fuelIgnited"];
            initialAttackApplied = (bool)state["initialAttackApplied"];
            damageApplied = (bool)state["damageApplied"];
            waterSuppressed = (bool)state["waterSuppressed"];
            ValidateRestoredState();
        }

        private void ValidateRestoredState()
        {
            Require(!(plainIgnited && fuelIgnited),
                "Causal fire owner restored two mutually exclusive ignition paths.");
            Require(!initialAttackApplied || plainIgnited,
                "Initial attack restored without its plain ignition predecessor.");
            Require(!damageApplied || fuelIgnited,
                "Fire damage restored without its physical-fuel ignition predecessor.");
            Require(!waterSuppressed || damageApplied,
                "Water suppression restored without its damage predecessor.");
            Require(!HasStarted()
                    || fixture.FireRuntime.ActiveFires.Count
                        + fixture.FireRuntime.History.Count == 1,
                "Causal fire owner restored an invalid fire identity count.");
        }

        private static JObject Serialize(object value, string label)
        {
            try
            {
                return JObject.Parse(JsonUtility.ToJson(
                    value ?? throw new ArgumentNullException(nameof(value))));
            }
            catch (Exception exception) when (exception is JsonException)
            {
                throw new InvalidOperationException(
                    "Could not serialize causal " + label + ".",
                    exception);
            }
        }

        private static T Deserialize<T>(JToken value, string label)
            where T : class
        {
            if (value is not JObject json)
                throw new InvalidOperationException(
                    "Causal " + label + " checkpoint must be an object.");
            T result = JsonUtility.FromJson<T>(json.ToString(Formatting.None));
            return result ?? throw new InvalidOperationException(
                "Causal " + label + " checkpoint deserialized to null.");
        }

        private static void RequireFields(
            JObject value,
            string label,
            params string[] expected)
        {
            string[] actual = value.Properties()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            string[] wanted = expected
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Require(actual.SequenceEqual(wanted, StringComparer.Ordinal),
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static T Proxy<T>() where T : class =>
        DispatchProxy.Create<T, DefaultInterfaceProxy>();

    public class DefaultInterfaceProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            Type returnType = targetMethod.ReturnType;
            if (returnType == typeof(void))
                return null;
            return returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject workerObject;
        private readonly CharacterSO workerData;
        private readonly CharacterAiWorldRegistry world;
        private readonly DomainFactDispatcher facts;

        internal Fixture(string ledgerRunId =
            "run:environment-fire-physical-production-entry")
        {
            var josa = new KoreanJosaFormatter();
            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new FireIgnitionOutcomeDescriptor(josa),
                    new FireSuppressionOutcomeDescriptor(josa),
                    new FireDamageOutcomeDescriptor(josa),
                    new FireFuelOutcomeDescriptor(josa),
                    new FireWaterOutcomeDescriptor(josa)
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new EnvironmentalFireIgnitionOutcomeAdapter(),
                    new EnvironmentalFireSuppressionOutcomeAdapter(),
                    new EnvironmentalFireDamageOutcomeAdapter(),
                    new EnvironmentalFireFuelOutcomeAdapter(),
                    new EnvironmentalFireWaterOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(ledgerRunId),
                1L);
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                registry,
                new GameEventBus());
            facts = new DomainFactDispatcher();
            GameplayDomainFactCommitGroup commitGroup = new(recorder, Ledger);
            facts.Subscribe(
                "log:environment.fire-damage",
                new GameplayDomainFactObserver<
                    DungeonStory.Environment.EnvironmentalFireDamageFact,
                    EnvironmentalFireDamageOutcomeReceipt>(
                        EnvironmentalFireDamageFactMapping.Capture,
                        registry,
                        Ledger,
                        recorder,
                        commitGroup,
                        EnvironmentalFireDamageFactMapping.Reserve));
            facts.Subscribe(
                "log:environment.fire-ignition",
                new GameplayDomainFactObserver<
                    DungeonStory.Environment.EnvironmentalFireIgnitedFact,
                    EnvironmentalFireIgnitionOutcomeReceipt>(
                        EnvironmentalFireActionFactMapping.Capture,
                        registry,
                        Ledger,
                        recorder,
                        commitGroup));
            facts.Subscribe(
                "log:environment.fire-suppression",
                new GameplayDomainFactObserver<
                    DungeonStory.Environment.EnvironmentalFireSuppressedFact,
                    EnvironmentalFireSuppressionOutcomeReceipt>(
                        EnvironmentalFireActionFactMapping.Capture,
                        registry,
                        Ledger,
                        recorder,
                        commitGroup));
            facts.Subscribe(
                "log:environment.fire-fuel-loss",
                new GameplayDomainFactObserver<
                    EnvironmentalFireFuelLostFact,
                    EnvironmentalFireFuelOutcomeReceipt>(
                        EnvironmentalFireResourceFactMapping.Capture,
                        registry,
                        Ledger,
                        recorder,
                        commitGroup));
            facts.Subscribe(
                "log:environment.fire-water-consumed",
                new GameplayDomainFactObserver<
                    EnvironmentalFireWaterConsumedFact,
                    EnvironmentalFireWaterOutcomeReceipt>(
                        EnvironmentalFireResourceFactMapping.Capture,
                        registry,
                        Ledger,
                        recorder,
                        commitGroup));
            Items = PhysicalItemDebugScenarios
                .CreateRuntimeForCrossDomainFixture(
                    out WorldItemRepository repository,
                    out _,
                    out ItemQuantityReservationService quantityReservations,
                    out _);
            Repository = repository;
            QuantityReservations = quantityReservations;
            PhysicalItemBatchDispositionService dispositions = new(
                Repository,
                Items.MassQuery,
                EditorNullItemMarkerPresenter.Instance,
                QuantityReservations,
                Items.CatalogProvider,
                null,
                null,
                new GameplayDomainFactDeliveryQuery(recorder, Ledger),
                null);
            ResourceTransaction = new EnvironmentalFireResourceTransaction(
                facts,
                facts,
                dispositions);

            world = new CharacterAiWorldRegistry(
                new SceneRuntimeRegistry<CharacterActor>(),
                new SceneRuntimeRegistry<WildlifeActor>(),
                new SceneRuntimeRegistry<BuildableObject>(),
                new SceneRuntimeRegistry<IWarehouseFacility>(),
                new SceneRuntimeRegistry<IRetailFacility>(),
                Proxy<IGameSessionStateProvider>(),
                Proxy<IRestoreWorldCandidateQuery>(),
                Proxy<ICharacterLifePublicationService>());
            workerObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                "EnvironmentFirePhysicalWorker");
            string workerDisplayName =
                "\uD654\uC7AC \uC9C4\uC555\uC6D0";
            workerData = CharacterAiPlanDebugFixtures.CreateCharacterData(
                CharacterType.NPC,
                workerDisplayName,
                "Slime");
            Worker = workerObject.GetComponent<CharacterActor>();
            Worker.Initialize(workerData);
            Worker.Progression.GrowthState.displayName = workerDisplayName;
            Require(string.Equals(
                    Worker.Identity.DisplayName,
                    workerDisplayName,
                    StringComparison.Ordinal),
                "Causal fire worker display name did not bind to the runtime identity.");
            world.RegisterCharacter(Worker);
            WorkerId = CharacterPersistentIdentity.Require(Worker);

            DamageAuthority = new EnvironmentalFireDamageAuthority(facts, facts);
            PhysicalAdapter = new EnvironmentalFireWorldAdapter(
                world,
                world,
                Proxy<IGridSystemProvider>(),
                Proxy<IBuildingStructuralIntegrityRuntime>(),
                Proxy<IWorldHazardZoneQuery>(),
                Proxy<IPowerInfrastructureQuery>(),
                QuantityReservations,
                Items,
                dispositions,
                dispositions,
                dispositions,
                ResourceTransaction,
                EditorFixedGameCalendar.Instance,
                DamageAuthority,
                Proxy<ICharacterBodyHealthMutationTransaction>());

            FireWorld = new FireWorldFixture(DamageAuthority);
            FireRuntime = new EnvironmentalFireRuntime(
                FireWorld,
                FireWorld,
                FireWorld,
                FireWorld,
                PhysicalAdapter,
                new EnvironmentalFireRuntimeSettings(5f, 0.35f, 0.02f),
                new RandomStreamProvider(73117),
                facts,
                facts,
                facts,
                EditorFixedGameCalendar.Instance,
                fuelLoss: PhysicalAdapter);
        }

        internal string CauseId { get; } =
            "qa:environment-fire-physical:fuel-ignition";
        internal WorldItemStackRuntime Items { get; }
        internal WorldItemRepository Repository { get; }
        internal ItemQuantityReservationService QuantityReservations { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal EnvironmentalFireDamageAuthority DamageAuthority { get; }
        internal EnvironmentalFireResourceTransaction ResourceTransaction { get; }
        internal CharacterActor Worker { get; }
        internal CharacterId WorkerId { get; }
        internal EnvironmentalFireWorldAdapter PhysicalAdapter { get; }
        internal FireWorldFixture FireWorld { get; }
        internal EnvironmentalFireRuntime FireRuntime { get; }

        internal GameplayOutcomeReplayIdentity RequirePublishedIdentity(
            string producerId,
            GameplayOutcomeTypeId outcomeTypeId)
        {
            GameplayOutcomeSnapshot snapshot = Ledger.CaptureGameplayOutcomes()
                .exactOutcomes.SingleOrDefault(value => value != null
                    && string.Equals(value.producerId, producerId,
                        StringComparison.Ordinal)
                    && string.Equals(value.outcomeTypeId, outcomeTypeId.Value,
                        StringComparison.Ordinal));
            Require(snapshot != null
                    && snapshot.lifecycle
                        == GameplayOutcomeRecordLifecycle.PublishedAcknowledged,
                producerId + "/" + outcomeTypeId.Value
                + " has no exact acknowledged outcome.");
            GameplayResultKey key = new(
                producerId,
                new GameplayOperationId(snapshot.operationId),
                snapshot.ownerRevision,
                snapshot.localResultIndex);
            Require(Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.OutcomeId.IsValid
                && identity.HasCanonicalPayloadHash,
                producerId + "/" + outcomeTypeId.Value
                + " lost its replay identity.");
            return identity;
        }

        internal int CountExact(
            string producerId,
            GameplayOutcomeTypeId outcomeTypeId) =>
            Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(value =>
                value != null
                && string.Equals(value.producerId, producerId,
                    StringComparison.Ordinal)
                && string.Equals(value.outcomeTypeId, outcomeTypeId.Value,
                    StringComparison.Ordinal));

        internal void RequireAcknowledged(
            string producerId,
            GameplayOutcomeTypeId outcomeTypeId,
            string operationId,
            GameplayOutcomeStatus expectedStatus)
        {
            GameplayOutcomeSnapshot snapshot = Ledger.CaptureGameplayOutcomes()
                .exactOutcomes.SingleOrDefault(value => value != null
                    && string.Equals(value.producerId, producerId,
                        StringComparison.Ordinal)
                    && string.Equals(value.outcomeTypeId, outcomeTypeId.Value,
                        StringComparison.Ordinal));
            Require(
                snapshot != null
                && string.Equals(snapshot.operationId, operationId,
                    StringComparison.Ordinal)
                && snapshot.commitRevision > 0L
                && snapshot.ownerRevision == snapshot.commitRevision
                && snapshot.localResultIndex == 0
                && snapshot.status == expectedStatus
                && snapshot.lifecycle
                    == GameplayOutcomeRecordLifecycle.PublishedAcknowledged,
                $"{producerId}/{outcomeTypeId.Value} lost its exact operation, revision, status, or PublishedAcknowledged lifecycle.");
            GameplayResultKey key = new(
                producerId,
                new GameplayOperationId(operationId),
                snapshot.commitRevision,
                0);
            Require(
                Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.ResultKey.Equals(key)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact != null
                && string.Equals(exact.producerId, producerId,
                    StringComparison.Ordinal)
                && string.Equals(exact.outcomeTypeId, outcomeTypeId.Value,
                    StringComparison.Ordinal)
                && string.Equals(exact.operationId, operationId,
                    StringComparison.Ordinal)
                && exact.ownerRevision == snapshot.ownerRevision,
                $"{producerId}/{outcomeTypeId.Value} replay identity or exact ledger payload is missing.");
        }

        public void Dispose()
        {
            facts.Dispose();
            if (Worker != null)
                world.UnregisterCharacter(Worker);
            CharacterAiPlanDebugFixtures.DestroyProbeObject(workerObject);
            if (workerData != null)
                UnityEngine.Object.DestroyImmediate(workerData);
            Items?.Dispose();
        }
    }

    private sealed class FireWorldFixture :
        IEnvironmentalFireTargetQuery,
        IEnvironmentalFireDamageCommand,
        IEnvironmentalFireSuppressionAccessQuery,
        IEnvironmentalFireElectricalSafetyQuery
    {
        private readonly EnvironmentalFireTargetSnapshot snapshot;
        private readonly EnvironmentalFireDamageAuthority damageOutcomes;

        internal FireWorldFixture(
            EnvironmentalFireDamageAuthority damageOutcomes)
        {
            this.damageOutcomes = damageOutcomes
                ?? throw new ArgumentNullException(nameof(damageOutcomes));
            Target = new EnvironmentalFireTargetRef(
                EnvironmentalFireTargetKind.Building,
                "building:environment-fire-physical");
            Profile = new EnvironmentalFireProfile(
                fuelCapacity: 1f,
                maximumIntensity: 1f,
                growthPerTick: 0.05f,
                fuelConsumedPerTick: 0.1f,
                damagePerTick: 10f,
                minimumSpreadIntensity: 0.9f,
                spreadChancePerTick: 0f,
                spreadIgnitionMultiplier: 0.5f,
                waterSuppressionPerUnit: 0.25f);
            snapshot = new EnvironmentalFireTargetSnapshot(
                Target,
                TargetPosition,
                exists: true,
                combustible: true,
                hasElectricalHazard: false,
                profile: Profile,
                displayName: TargetDisplayName);
        }

        internal EnvironmentalFireTargetRef Target { get; }
        internal EnvironmentalFireProfile Profile { get; }
        internal Vector2Int TargetPosition { get; } = new(4, 4);
        internal Vector2Int StandPosition { get; } = new(3, 4);
        internal string TargetDisplayName { get; } =
            "\uBB3C\uB9AC \uD654\uC7AC \uC2DC\uD5D8 \uC2DC\uC124";
        internal float HitPoints { get; private set; } = 100f;

        internal void RestoreHitPoints(float value)
        {
            if (!float.IsFinite(value) || value <= 0f || value > 100f)
                throw new InvalidOperationException(
                    "Causal fire target hit points are invalid.");
            HitPoints = value;
        }

        public bool TryGetTarget(
            EnvironmentalFireTargetRef target,
            out EnvironmentalFireTargetSnapshot result)
        {
            result = snapshot;
            return target.Equals(Target);
        }

        public IReadOnlyList<EnvironmentalFireTargetSnapshot> GetAdjacentTargets(
            EnvironmentalFireTargetRef target) =>
            Array.Empty<EnvironmentalFireTargetSnapshot>();

        public bool TryApply(
            EnvironmentalFireDamageCommand command,
            out EnvironmentalFireDamageResult result)
        {
            if (!command.Target.Equals(Target))
            {
                result = new EnvironmentalFireDamageResult(
                    false,
                    0f,
                    true,
                    "environmental-fire-causal-target-mismatch");
                return false;
            }
            bool began = damageOutcomes.TryBegin(
                command,
                TargetDisplayName,
                EditorFixedGameCalendar.Instance.Day,
                out _,
                out string beginFailure);
            if (!began)
            {
                result = new EnvironmentalFireDamageResult(
                    false,
                    0f,
                    true,
                    beginFailure);
                return false;
            }
            float before = HitPoints;
            float applied = Math.Min(before, command.RequestedDamage);
            HitPoints = Math.Max(0f, before - applied);
            if (damageOutcomes.TryCommitApplied(
                    command.OperationId,
                    applied,
                    HitPoints > 0f,
                    out result,
                    out string commitFailure))
            {
                return true;
            }
            HitPoints = before;
            damageOutcomes.CancelUncommitted(command.OperationId);
            result = new EnvironmentalFireDamageResult(
                false,
                0f,
                true,
                commitFailure);
            return false;
        }

        public bool CanSuppress(
            string workerId,
            Vector2Int standPosition,
            EnvironmentalFireTargetRef target,
            out string failureReason)
        {
            bool allowed = !string.IsNullOrWhiteSpace(workerId)
                && standPosition == StandPosition
                && target.Equals(Target);
            failureReason = allowed
                ? string.Empty
                : "environment-fire-physical-access-invalid";
            return allowed;
        }

        public bool TryGetSafety(
            EnvironmentalFireTargetRef target,
            out EnvironmentalFireElectricalSafetySnapshot result)
        {
            result = new EnvironmentalFireElectricalSafetySnapshot(
                Target,
                hasElectricalContact: false,
                connectionEnabled: false,
                breakerTripped: true,
                hasLiveGeneration: false,
                hasStoredPower: false);
            return target.Equals(Target);
        }
    }
}
#endif
