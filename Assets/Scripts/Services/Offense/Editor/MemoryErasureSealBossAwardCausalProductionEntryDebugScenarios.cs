#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:memory-erasure-seal-boss-award";
    internal const string FirstAwardActionId =
        "action:item-memory-erasure-seal-first-boss-awarded";
    internal const string SecondAwardActionId =
        "action:item-memory-erasure-seal-second-boss-awarded";

    private const string OwnerKind =
        "owner-kind:memory-erasure-seal-boss-award";
    private const string DomainStateSchema =
        "dungeonstory-causal-memory-erasure-seal-boss-award-owner-state@2";
    private const int AbsoluteDay = 42;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
        {
            throw new ArgumentException(
                "A causal run ID is required.", nameof(causalRunId));
        }
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A boss-award checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                FirstAwardActionId,
                "item",
                EvolutionOutcomeIds.MemoryErasureBossAwarded,
                context.CaptureState,
                context.AwardFirstRegion,
                context.RequireFirstOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                SecondAwardActionId,
                "item",
                EvolutionOutcomeIds.MemoryErasureBossAwarded,
                () => new Dictionary<string, object>(),
                context.AwardSecondRegion,
                context.RequireSecondOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string ownerId = "owner:causal-memory-erasure-boss-award:focused";
        JObject firstCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-memory-erasure-boss-award:first",
                   ownerId)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstAwardActionId);
            IReadOnlyDictionary<string, object> before = first.CaptureState();
            Require(
                (int)before["bossAward.stage"] == 0
                && (int)before["bossAward.physicalPublicationCount"] == 0
                && (int)before["bossAward.exactOutcomeCount"] == 0,
                "Boss-award owner did not start empty.");
            Require(first.Execute(),
                "The first real region-boss award was rejected.");
            Require(first.RequireExactOutcome().HasCanonicalPayloadHash,
                "The first boss award has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require(
                (int)after["bossAward.stage"] == 1
                && (bool)after["bossAward.firstAwarded"]
                && !(bool)after["bossAward.secondAwarded"]
                && (int)after["bossAward.totalSealQuantity"] == 1
                && (int)after["bossAward.exactOutcomeCount"] == 1,
                "The first boss award did not retain region, item and ledger state.");
            firstCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-memory-erasure-boss-award:second",
                   ownerId,
                   firstCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstAwardActionId);
            IProductionCausalActionFixture second = fixtures.Get(
                SecondAwardActionId);
            IReadOnlyDictionary<string, object> restored = first.CaptureState();
            Require(
                (int)restored["bossAward.stage"] == 1
                && (bool)restored["bossAward.firstAwarded"]
                && !first.Execute(),
                "Fresh composition restore lost or replayed the first boss award.");
            Require(second.Execute(),
                "The second real region-boss award was rejected.");
            Require(second.RequireExactOutcome().HasCanonicalPayloadHash,
                "The second boss award has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require(
                (int)after["bossAward.stage"] == 2
                && (bool)after["bossAward.firstAwarded"]
                && (bool)after["bossAward.secondAwarded"]
                && (int)after["bossAward.totalSealQuantity"] == 2
                && (int)after["bossAward.exactOutcomeCount"] == 2,
                "The second boss award did not preserve the first award.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)second)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-memory-erasure-boss-award:terminal",
                   ownerId,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstAwardActionId);
            IProductionCausalActionFixture second = fixtures.Get(
                SecondAwardActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
            Require(!first.Execute() && !second.Execute(),
                "Terminal boss-award owner was not idempotent.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected boss-award replay mutated the owner checkpoint.");
        }

        JObject identityTamper = (JObject)firstCheckpoint.DeepClone();
        ((JObject)identityTamper["domainState"])["firstRegionId"] =
            OffenseRegionRuntime.SealedZoneRegionId;
        Require(RejectCheckpoint(ownerId, identityTamper, "identity-tamper"),
            "A tampered boss-award region identity was accepted.");

        JObject publicationTamper = (JObject)firstCheckpoint.DeepClone();
        JObject publicationState = (JObject)
            ((JObject)publicationTamper["domainState"])["publicationState"];
        ((JArray)publicationState["operations"]).Clear();
        Require(RejectCheckpoint(ownerId, publicationTamper, "publication-tamper"),
            "A boss-award publication/region/ledger mismatch was accepted.");

        JObject observationTamper = (JObject)firstCheckpoint.DeepClone();
        JArray observations = (JArray)((JObject)
            ((JObject)observationTamper["domainState"])[
                "bossAwardPublications"])["observations"];
        ((JObject)observations[0])["publicationKey"] =
            "tampered:memory-erasure-boss-award-publication";
        Require(RejectCheckpoint(ownerId, observationTamper, "observation-tamper"),
            "A boss-award observation/ledger mismatch was accepted.");

        if (log)
        {
            Debug.Log(
                "Memory-erasure seal first region boss -> restored second region boss award causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Memory-erasure boss-award causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static bool RejectCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-memory-erasure-boss-award:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly OffenseRegionRuntime regions;
        private readonly PublicationFixture publications;
        private readonly EvolutionGameplayOutcomeEditorFixture outcomes;
        private readonly MemoryErasureSealBossAwardService service;
        private bool disposed;

        internal Context(
            string causalRunId,
            string ownerId,
            JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":memory-erasure-boss-award-ledger";
            regions = new OffenseRegionRuntime();
            publications = new PublicationFixture();
            outcomes = new EvolutionGameplayOutcomeEditorFixture(
                ledgerRunId,
                (AbsoluteDay * GameCalendarRules.SecondsPerDay)
                    + (8f * GameCalendarRules.SecondsPerDay / 24f),
                calendar: new EditorFixedGameCalendar(AbsoluteDay));
            service = new MemoryErasureSealBossAwardService(
                new DungeonStory.Foundation.UnityUiClock(),
                regions,
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOffenseWorldSimulation>(),
                publications,
                DropZoneFixture.Instance,
                outcomes.BossAwards,
                outcomes.Calendar);

            if (checkpoint != null)
                RestoreOwnerCheckpoint(checkpoint);
            ValidateState();
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string PersistentOwnerId => ownerId;
        internal string PersistentOwnerKind => HasPersistentOwner
            ? OwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            ValidateState();
            return new Dictionary<string, object>
            {
                ["bossAward.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-memory-erasure-boss-award",
                ["bossAward.firstRegionId"] =
                    OffenseRegionRuntime.BorderTradeRegionId,
                ["bossAward.secondRegionId"] =
                    OffenseRegionRuntime.RivalOutpostRegionId,
                ["bossAward.stage"] = Stage,
                ["bossAward.absoluteDay"] = AbsoluteDay,
                ["bossAward.awardedRegionCount"] = AwardedRegionCount,
                ["bossAward.physicalPublicationCount"] = publications.Count,
                ["bossAward.firstAwarded"] = FirstAwarded,
                ["bossAward.secondAwarded"] = SecondAwarded,
                ["bossAward.firstSealQuantity"] = publications.GetQuantity(
                    FirstOperationId),
                ["bossAward.secondSealQuantity"] = publications.GetQuantity(
                    SecondOperationId),
                ["bossAward.totalSealQuantity"] = publications.TotalQuantity,
                ["bossAward.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool AwardFirstRegion() => AwardRegion(
            OffenseRegionRuntime.BorderTradeRegionId,
            "expedition:causal-memory-erasure:first",
            expectedStage: 0,
            expectedOutcomeCount: 1);

        internal bool AwardSecondRegion() => AwardRegion(
            OffenseRegionRuntime.RivalOutpostRegionId,
            "expedition:causal-memory-erasure:second",
            expectedStage: 1,
            expectedOutcomeCount: 2);

        internal GameplayOutcomeReplayIdentity RequireFirstOutcome() =>
            RequireOutcome(FirstOperationId, expectedCount: 1, throwOnFailure: true);

        internal GameplayOutcomeReplayIdentity RequireSecondOutcome() =>
            RequireOutcome(SecondOperationId, expectedCount: 2, throwOnFailure: true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral boss-award fixtures have no checkpoint.");
            }
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = DomainStateSchema,
                    ["firstRegionId"] =
                        OffenseRegionRuntime.BorderTradeRegionId,
                    ["secondRegionId"] =
                        OffenseRegionRuntime.RivalOutpostRegionId,
                    ["stage"] = Stage,
                    ["regionState"] = JObject.FromObject(regions.Capture()),
                    ["publicationState"] = publications.Capture(),
                    ["bossAwardPublications"] =
                        CaptureBossAwardPublications()
                },
                outcomes.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            outcomes.Dispose();
        }

        private string FirstOperationId =>
            MemoryErasureSealBossAwardRules.BuildOperationId(
                OffenseRegionRuntime.BorderTradeRegionId);

        private string SecondOperationId =>
            MemoryErasureSealBossAwardRules.BuildOperationId(
                OffenseRegionRuntime.RivalOutpostRegionId);

        private bool FirstAwarded => IsAwarded(
            OffenseRegionRuntime.BorderTradeRegionId,
            FirstOperationId);

        private bool SecondAwarded => IsAwarded(
            OffenseRegionRuntime.RivalOutpostRegionId,
            SecondOperationId);

        private int AwardedRegionCount => regions.Regions.Count(value =>
            value != null && value.memoryErasureSealAwardPublished);

        private int Stage => SecondAwarded ? 2 : FirstAwarded ? 1 : 0;

        private bool AwardRegion(
            string regionId,
            string expeditionId,
            int expectedStage,
            int expectedOutcomeCount)
        {
            ThrowIfDisposed();
            if (Stage != expectedStage)
                return false;
            OffenseTargetDefinition target = new()
            {
                id = "target:" + expeditionId,
                title = expeditionId,
                regionId = regionId,
                durationSeconds = 1f,
                requiredMembers = 1
            };
            OffenseExpeditionRun expedition = new(
                expeditionId,
                target,
                Array.Empty<CharacterActor>(),
                0f);
            OffenseRouteNode boss = expedition.Route.Nodes.Single(value =>
                value.IsBoss);
            MemoryErasureSealBossAwardResult result = service.TryAwardForVictory(
                expedition,
                boss,
                worldObjectiveBattle: false);
            string operationId =
                MemoryErasureSealBossAwardRules.BuildOperationId(regionId);
            if (result.Status != MemoryErasureSealBossAwardStatus.Awarded
                || !string.Equals(result.RegionId, regionId, StringComparison.Ordinal)
                || !string.Equals(
                    result.OperationId,
                    operationId,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(result.PhysicalCommitId)
                || publications.GetQuantity(operationId)
                    != MemoryErasureSealItemRules.UseQuantity
                || !RequireOutcome(
                    operationId,
                    expectedOutcomeCount,
                    throwOnFailure: false).HasCanonicalPayloadHash)
            {
                return false;
            }
            ValidateState();
            return true;
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(
            string operationId,
            int expectedCount,
            bool throwOnFailure)
        {
            var key = new GameplayResultKey(
                EvolutionOutcomeIds.MemoryErasureBossAwardProducerId,
                new GameplayOperationId(operationId),
                0L,
                0);
            GameplayOutcomeReplayIdentity identity = default;
            bool valid = ExactOutcomeCount() == expectedCount
                && outcomes.Ledger.TryGetResultIdentity(
                    key,
                    out identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact != null
                && string.Equals(
                    exact.outcomeTypeId,
                    EvolutionOutcomeIds.MemoryErasureBossAwarded.Value,
                    StringComparison.Ordinal);
            if (!valid && throwOnFailure)
            {
                throw new InvalidOperationException(
                    "Causal boss-award production entry did not publish its exact outcome.");
            }
            return valid ? identity : default;
        }

        private int ExactOutcomeCount() => outcomes.Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes
            .Count(value => value != null
                && string.Equals(
                    value.producerId,
                    EvolutionOutcomeIds.MemoryErasureBossAwardProducerId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.outcomeTypeId,
                    EvolutionOutcomeIds.MemoryErasureBossAwarded.Value,
                    StringComparison.Ordinal));

        private bool IsAwarded(string regionId, string operationId)
        {
            OffenseRegionState region = regions.Regions.Single(value =>
                string.Equals(value.regionId, regionId, StringComparison.Ordinal));
            return region.memoryErasureSealAwardPublished
                && string.Equals(
                    region.memoryErasureSealAwardOperationId,
                    operationId,
                    StringComparison.Ordinal);
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                outcomes.Ledger);
            RequireFields(
                state,
                "memory-erasure boss-award owner state",
                "schemaVersion",
                "schema",
                "firstRegionId",
                "secondRegionId",
                "stage",
                "regionState",
                "publicationState",
                "bossAwardPublications");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals(
                    (string)state["schema"],
                    DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["firstRegionId"],
                    OffenseRegionRuntime.BorderTradeRegionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["secondRegionId"],
                    OffenseRegionRuntime.RivalOutpostRegionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Boss-award checkpoint identity changed.");
            }
            DungeonOffenseRegionSaveData regionState =
                state["regionState"].ToObject<DungeonOffenseRegionSaveData>()
                ?? throw new InvalidOperationException(
                    "Boss-award region state is missing.");
            regions.PublishRestoreCandidate(
                regions.BuildRestoreCandidate(regionState));
            publications.Restore((JObject)state["publicationState"]);
            string bossAwardPublicationJson = state["bossAwardPublications"]
                .ToString(Newtonsoft.Json.Formatting.None);
            MemoryErasureBossAwardPublicationSaveSection.ValidateJson(
                bossAwardPublicationJson);
            MemoryErasureBossAwardPublicationSaveData savedBossAwards =
                MemoryErasureBossAwardPublicationRuntime.ValidateAndClone(
                    DungeonSaveJson.Deserialize<
                        MemoryErasureBossAwardPublicationSaveData>(
                            bossAwardPublicationJson));
            GameplayOutcomeLedgerSaveData canonicalLedger =
                outcomes.Ledger.CaptureGameplayOutcomes();
            foreach (MemoryErasureBossAwardPublicationData observation
                in savedBossAwards.observations)
            {
                MemoryErasureBossAwardFactPreflight.ValidateObservation(
                    observation,
                    canonicalLedger);
            }
            if (savedBossAwards.observations.Count != Stage
                || savedBossAwards.observations.Any(observation =>
                    !observation.delivered
                    || !IsAwarded(
                        observation.fact.regionId,
                        observation.fact.operationId)))
            {
                throw new InvalidOperationException(
                    "Boss-award observation journal disagrees with its region owner state.");
            }
            outcomes.BossAwards.Restore(savedBossAwards);
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Boss-award checkpoint stage disagrees with its region state.");
            }
        }

        private JObject CaptureBossAwardPublications()
        {
            string json = new MemoryErasureBossAwardPublicationSaveSection(
                    outcomes.BossAwards)
                .Capture();
            MemoryErasureBossAwardPublicationSaveSection.ValidateJson(json);
            return JObject.Parse(json);
        }

        private void ValidateState()
        {
            int expectedStage = Stage;
            bool structurallyValid = expectedStage is >= 0 and <= 2
                && AwardedRegionCount == expectedStage
                && publications.Count == expectedStage
                && publications.TotalQuantity == expectedStage
                    * MemoryErasureSealItemRules.UseQuantity
                && ExactOutcomeCount() == expectedStage
                && publications.GetQuantity(FirstOperationId)
                    == (FirstAwarded
                        ? MemoryErasureSealItemRules.UseQuantity
                        : 0)
                && publications.GetQuantity(SecondOperationId)
                    == (SecondAwarded
                        ? MemoryErasureSealItemRules.UseQuantity
                        : 0)
                && (!SecondAwarded || FirstAwarded)
                && regions.CapturePendingMemoryErasureSealAwardRegionIds()
                    .Count == 0;
            if (!structurallyValid)
            {
                throw new InvalidOperationException(
                    "Boss-award region, physical publication and exact ledger state diverged.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    "MemoryErasureSealBossAwardCausalContext");
            }
        }
    }

    private sealed class PublicationFixture :
        IPhysicalItemSourcePublicationService
    {
        private readonly Dictionary<string, int> outputByOperation =
            new(StringComparer.Ordinal);

        internal int Count => outputByOperation.Count;
        internal int TotalQuantity => outputByOperation.Values.Sum();

        internal int GetQuantity(string operationId) =>
            outputByOperation.TryGetValue(operationId, out int quantity)
                ? quantity
                : 0;

        internal JObject Capture() => new()
        {
            ["schemaVersion"] = 1,
            ["operations"] = new JArray(outputByOperation
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new JObject
                {
                    ["operationId"] = pair.Key,
                    ["quantity"] = pair.Value
                }))
        };

        internal void Restore(JObject checkpoint)
        {
            RequireFields(
                checkpoint,
                "memory-erasure boss-award publication state",
                "schemaVersion",
                "operations");
            if ((int)checkpoint["schemaVersion"] != 1
                || checkpoint["operations"] is not JArray rows)
            {
                throw new InvalidOperationException(
                    "Boss-award publication state is invalid.");
            }
            outputByOperation.Clear();
            foreach (JToken rowToken in rows)
            {
                if (rowToken is not JObject row)
                {
                    throw new InvalidOperationException(
                        "Boss-award publication row is invalid.");
                }
                RequireFields(
                    row,
                    "memory-erasure boss-award publication row",
                    "operationId",
                    "quantity");
                string operationId = (string)row["operationId"] ?? string.Empty;
                int quantity = (int)row["quantity"];
                bool expectedOperation = string.Equals(
                        operationId,
                        MemoryErasureSealBossAwardRules.BuildOperationId(
                            OffenseRegionRuntime.BorderTradeRegionId),
                        StringComparison.Ordinal)
                    || string.Equals(
                        operationId,
                        MemoryErasureSealBossAwardRules.BuildOperationId(
                            OffenseRegionRuntime.RivalOutpostRegionId),
                        StringComparison.Ordinal);
                if (!expectedOperation
                    || quantity != MemoryErasureSealItemRules.UseQuantity
                    || !outputByOperation.TryAdd(operationId, quantity))
                {
                    throw new InvalidOperationException(
                        "Boss-award publication row conflicts with the closed recipe.");
                }
            }
        }

        public bool TryEnsureLooseOutputs(
            IReadOnlyDictionary<string, int> outputs,
            Vector2Int outputPosition,
            string operationId,
            string reasonCode,
            out PhysicalItemSourcePublicationReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = string.Empty;
            if (outputs == null
                || outputs.Count != 1
                || !outputs.TryGetValue(
                    MemoryErasureSealItemRules.ItemId,
                    out int quantity)
                || quantity != MemoryErasureSealItemRules.UseQuantity
                || !string.Equals(
                    reasonCode,
                    MemoryErasureSealBossAwardRules.PublicationReason,
                    StringComparison.Ordinal))
            {
                failureReason = "causal-boss-award-publication-request-invalid";
                return false;
            }
            if (outputByOperation.TryGetValue(
                    operationId,
                    out int existingQuantity)
                && existingQuantity != quantity)
            {
                failureReason = "causal-boss-award-publication-conflict";
                return false;
            }
            outputByOperation[operationId] = quantity;
            receipt = new PhysicalItemSourcePublicationReceipt(
                operationId,
                reasonCode,
                new[] { "causal-physical-commit:" + operationId },
                quantity,
                quantity);
            return receipt.IsCommitted;
        }
    }

    private sealed class DropZoneFixture : IWorldDropZoneQuery
    {
        internal static readonly DropZoneFixture Instance = new();
        private static readonly Vector2Int Dropoff = new(3, 4);

        public bool TryGetDeliveryDropoff(out Vector2Int position)
        {
            position = Dropoff;
            return true;
        }

        public bool TryGetExpeditionLootDropoff(out Vector2Int position)
        {
            position = Dropoff;
            return true;
        }

        public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint)
        {
            entryPoint = default;
            return false;
        }
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IProductionCausalActionFixture[] fixtures;

        internal FixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> fixtures)
        {
            this.fixtures = fixtures?.ToArray()
                ?? throw new ArgumentNullException(nameof(fixtures));
            Require(this.fixtures.Length == 2,
                "Boss-award causal fixture set is incomplete.");
        }

        internal IProductionCausalActionFixture Get(string actionId) =>
            fixtures.Single(value => string.Equals(
                value.ActionId,
                actionId,
                StringComparison.Ordinal));

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] fields)
    {
        if (value == null)
            throw new InvalidOperationException(label + " is missing.");
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToArray();
        string[] expected = fields
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields are not closed. expected="
                + string.Join(",", expected)
                + "; actual=" + string.Join(",", actual));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
