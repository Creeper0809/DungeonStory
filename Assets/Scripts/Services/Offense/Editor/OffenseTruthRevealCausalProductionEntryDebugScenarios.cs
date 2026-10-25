#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public static class OffenseTruthRevealCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:offense-truth-reveal";
    internal const string PreliminaryVictoryActionId =
        "action:offense-truth-preliminary-expedition-victory";
    internal const string RevealTruthActionId =
        "action:offense-truth-final-target-revealed";

    private const string OwnerKind = "owner-kind:offense-truth-reveal";
    private const string DomainStateSchema =
        "dungeonstory-causal-offense-truth-reveal-owner-state@1";
    private const string PreliminaryTargetId =
        "target:causal-truth:outer-record";
    private const string TruthTargetId =
        "target:causal-truth:buried-capital";
    private const string PreliminaryExpeditionId =
        "expedition:causal-truth:outer-record";
    private const int AbsoluteDay = 31;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException(
                "A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "An offense truth-reveal checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        MigratedProducerOutcomeDefinition preliminaryDefinition =
            MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind.OffenseExpeditionResult);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                PreliminaryVictoryActionId,
                "offense",
                preliminaryDefinition.OutcomeTypeId,
                context.CaptureState,
                context.CompletePreliminaryExpedition,
                context.RequirePreliminaryOutcome,
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
                RevealTruthActionId,
                "offense",
                ExternalFactionOutcomeIds.TruthRevealed,
                () => new Dictionary<string, object>(),
                context.RevealTruth,
                context.RequireTruthOutcome,
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
        const string ownerId = "owner:causal-offense-truth-reveal:focused";
        JObject firstCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-offense-truth-reveal:first",
                   ownerId)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                PreliminaryVictoryActionId);
            IReadOnlyDictionary<string, object> before = first.CaptureState();
            Require(
                (int)before["offenseTruth.stage"] == 0
                && (int)before["offenseTruth.completedTargetCount"] == 0
                && (int)before["offenseTruth.exactOutcomeCount"] == 0,
                "Offense truth-reveal owner did not start empty.");
            Require(first.Execute(),
                "The preliminary production expedition was rejected.");
            Require(first.RequireExactOutcome().HasCanonicalPayloadHash,
                "The preliminary expedition result has no canonical hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require(
                (int)after["offenseTruth.stage"] == 1
                && (bool)after["offenseTruth.preliminaryCompleted"]
                && !(bool)after["offenseTruth.truthRevealed"]
                && (int)after["offenseTruth.completedTargetCount"] == 1
                && (int)after["offenseTruth.exactOutcomeCount"] == 1,
                "The preliminary expedition did not advance and retain the campaign.");
            firstCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-offense-truth-reveal:second",
                   ownerId,
                   firstCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                PreliminaryVictoryActionId);
            IProductionCausalActionFixture reveal = fixtures.Get(
                RevealTruthActionId);
            IReadOnlyDictionary<string, object> restored = first.CaptureState();
            Require(
                (int)restored["offenseTruth.stage"] == 1
                && (bool)restored["offenseTruth.preliminaryCompleted"]
                && !first.Execute(),
                "Fresh composition restore lost or replayed the preliminary expedition.");
            Require(reveal.Execute(),
                "The actual final truth target command was rejected.");
            Require(reveal.RequireExactOutcome().HasCanonicalPayloadHash,
                "The truth-reveal result has no canonical hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require(
                (int)after["offenseTruth.stage"] == 2
                && (bool)after["offenseTruth.preliminaryCompleted"]
                && (bool)after["offenseTruth.truthCompleted"]
                && (bool)after["offenseTruth.truthRevealed"]
                && string.Equals(
                    (string)after["offenseTruth.revealedTargetId"],
                    TruthTargetId,
                    StringComparison.Ordinal)
                && (int)after["offenseTruth.completedTargetCount"] == 2
                && (int)after["offenseTruth.exactOutcomeCount"] == 2,
                "The truth reveal did not preserve the preliminary campaign victory.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)reveal)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-offense-truth-reveal:terminal",
                   ownerId,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                PreliminaryVictoryActionId);
            IProductionCausalActionFixture reveal = fixtures.Get(
                RevealTruthActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
            Require(!first.Execute() && !reveal.Execute(),
                "Terminal offense truth owner was not idempotent.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected offense truth replay mutated the owner checkpoint.");
        }

        JObject campaignTamper = (JObject)firstCheckpoint.DeepClone();
        JObject campaignState = (JObject)
            ((JObject)campaignTamper["domainState"])["campaign"];
        ((JArray)campaignState["completedTargetIds"]).Clear();
        Require(RejectCheckpoint(ownerId, campaignTamper, "campaign-tamper"),
            "A truth-reveal checkpoint with missing campaign completion was accepted.");

        JObject revisionTamper = (JObject)firstCheckpoint.DeepClone();
        JObject migratedOwners = (JObject)
            ((JObject)revisionTamper["domainState"])["migratedOutcomeOwners"];
        ((JArray)migratedOwners["owners"]).Clear();
        Require(RejectCheckpoint(ownerId, revisionTamper, "revision-tamper"),
            "A truth-reveal checkpoint with missing outcome owner revision was accepted.");

        if (log)
        {
            Debug.Log(
                "Offense preliminary expedition -> restored final truth reveal causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Offense truth-reveal causal owner family returned false.");
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
                "run:causal-offense-truth-reveal:" + suffix,
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
        private readonly GameObject root;
        private readonly GameEventBus events;
        private readonly FixedCalendar calendar;
        private readonly OffenseCampaignRuntime campaign;
        private readonly OffenseWorldMapRuntime worldMap;
        private readonly OffenseRewardRuntime rewards;
        private readonly MetaProgressionRuntime meta;
        private readonly GameplayOutcomeLedger ledger;
        private readonly MigratedProducerOutcomeRuntime migratedOutcomes;
        private readonly EditorOffenseTruthFactFixture truthFacts;
        private readonly EditorOffenseExpeditionResultFactFixture resultFacts;
        private readonly OffenseExpeditionResultFinalizer finalizer;
        private readonly List<OffenseExpeditionResult> history = new();
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
                : causalRunId + ":offense-truth-ledger";

            events = new GameEventBus();
            calendar = new FixedCalendar(AbsoluteDay, 9);
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new OffenseTruthRevealOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new MigratedProducerOutcomeAdapter(),
                    new OffenseTruthRevealOutcomeAdapter()
                },
                new IGameplayOutcomeDescriptorCatalog[]
                {
                    new MigratedProducerOutcomeDescriptorCatalog(
                        new KoreanJosaFormatter())
                });
            ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 16,
                    largePageCount: 4,
                    knownResultKeyCapacity: 64,
                    retainedSmallPageCount: 16,
                    retainedLargePageCount: 4),
                new GameplayOutcomeRunId(ledgerRunId),
                1L);
            var recorder = new GameplayOutcomeRecorder(ledger, registry, events);
            migratedOutcomes = new MigratedProducerOutcomeRuntime(
                new DungeonRuntimeAggregateRootStore(),
                ledger,
                recorder,
                ledger);
            truthFacts = new EditorOffenseTruthFactFixture(
                events,
                registry,
                ledger,
                recorder);
            resultFacts = new EditorOffenseExpeditionResultFactFixture(
                events,
                migratedOutcomes,
                recorder,
                ledger);

            root = new GameObject("Causal Offense Truth Reveal Owner");
            rewards = root.AddComponent<OffenseRewardRuntime>();
            rewards.Construct(
                EmptyRewardContextBuilder.Instance,
                EmptyRewardGrantService.Instance);
            meta = root.AddComponent<MetaProgressionRuntime>();
            meta.Construct(
                new MetaRunResultBuilder(),
                EmptyMetaApplicationPort.Instance,
                new UnityGameClock(),
                EmptyMetaUpgradeCatalog.Instance,
                new DungeonRuntimeAggregateRootStore());
            campaign = new OffenseCampaignRuntime();
            worldMap = root.AddComponent<OffenseWorldMapRuntime>();
            worldMap.Construct(
                new DungeonStory.Foundation.UnityUiClock(),
                events,
                externalInfluence: null,
                campaign,
                campaign,
                new FixedCampaignCatalog(CreateTargets()),
                truthFacts.Facts,
                truthFacts.Facts,
                calendar);
            finalizer = new OffenseExpeditionResultFinalizer(
                new OffenseSceneRuntimeReferences(
                    worldMap,
                    rewards,
                    null,
                    null,
                    null),
                new ProgressionSceneRuntimeReferences(null, null, meta),
                worldMap);
            finalizer.ConstructFactPublication(
                calendar,
                resultFacts.Publications);

            try
            {
                if (checkpoint != null)
                    RestoreOwnerCheckpoint(checkpoint);
                ValidateState();
            }
            catch
            {
                Dispose();
                throw;
            }
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
                ["offenseTruth.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-offense-truth-reveal",
                ["offenseTruth.stage"] = Stage,
                ["offenseTruth.absoluteDay"] = calendar.Day,
                ["offenseTruth.knownTargetCount"] =
                    campaign.State.KnownTargetIds.Count,
                ["offenseTruth.completedTargetCount"] =
                    campaign.State.CompletedTargetCount,
                ["offenseTruth.preliminaryKnown"] =
                    campaign.State.KnowTarget(PreliminaryTargetId),
                ["offenseTruth.preliminaryCompleted"] =
                    campaign.State.IsTargetCompleted(PreliminaryTargetId),
                ["offenseTruth.truthKnown"] =
                    campaign.State.KnowTarget(TruthTargetId),
                ["offenseTruth.truthCompleted"] =
                    campaign.State.IsTargetCompleted(TruthTargetId),
                ["offenseTruth.truthRevealed"] = campaign.State.TruthRevealed,
                ["offenseTruth.revealedTargetId"] =
                    campaign.State.RevealedTruthTargetId,
                ["offenseTruth.migratedOwnerRevisionCount"] =
                    migratedOutcomes.Capture().owners.Count,
                ["offenseTruth.preliminaryOutcomeCount"] =
                    OutcomeCount(PreliminaryOutcomeTypeId),
                ["offenseTruth.truthOutcomeCount"] =
                    OutcomeCount(ExternalFactionOutcomeIds.TruthRevealed),
                ["offenseTruth.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool CompletePreliminaryExpedition()
        {
            ThrowIfDisposed();
            if (Stage != 0
                || !worldMap.TryGetTargetDefinition(
                    PreliminaryTargetId,
                    out OffenseTargetDefinition target))
            {
                return false;
            }

            var expedition = new OffenseExpeditionRun(
                PreliminaryExpeditionId,
                target,
                Array.Empty<CharacterActor>(),
                0f);
            var result = new OffenseExpeditionResult(
                expedition.ExpeditionId,
                target.id,
                target.title,
                success: true,
                totalPower: 14f,
                requiredPower: target.requiredPower,
                danger: target.danger,
                elapsedSeconds: 18f,
                Array.Empty<OffenseExpeditionMemberSnapshot>(),
                new[] { "외곽 기록소를 확보해 심부 진실 표적의 길을 열었다." });
            OffenseExpeditionResult committed = finalizer.Finalize(
                expedition,
                result,
                history);
            resultFacts.FlushNotifications();
            bool valid = committed != null
                && committed.success
                && history.Count == 1
                && campaign.State.IsTargetCompleted(PreliminaryTargetId)
                && !campaign.State.TruthRevealed
                && RequirePreliminaryOutcome(throwOnFailure: false)
                    .HasCanonicalPayloadHash;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool RevealTruth()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            bool revealed = worldMap.TryRecordSuccessfulExpedition(
                TruthTargetId,
                out OffenseTargetSnapshot completed,
                out _);
            bool valid = revealed
                && completed?.isCompleted == true
                && campaign.State.TruthRevealed
                && RequireTruthOutcome(throwOnFailure: false)
                    .HasCanonicalPayloadHash;
            if (valid)
                ValidateState();
            return valid;
        }

        internal GameplayOutcomeReplayIdentity RequirePreliminaryOutcome() =>
            RequirePreliminaryOutcome(throwOnFailure: true);

        internal GameplayOutcomeReplayIdentity RequireTruthOutcome() =>
            RequireTruthOutcome(throwOnFailure: true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral offense truth fixtures have no checkpoint.");
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
                    ["preliminaryTargetId"] = PreliminaryTargetId,
                    ["truthTargetId"] = TruthTargetId,
                    ["stage"] = Stage,
                    ["campaign"] = JObject.FromObject(campaign.Capture()),
                    ["migratedOutcomeOwners"] =
                        JObject.FromObject(migratedOutcomes.Capture())
                },
                ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            resultFacts.Dispose();
            truthFacts.Dispose();
            Object.DestroyImmediate(root);
        }

        private GameplayOutcomeTypeId PreliminaryOutcomeTypeId =>
            MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind.OffenseExpeditionResult)
            .OutcomeTypeId;

        private int Stage => campaign.State.TruthRevealed
            ? 2
            : campaign.State.IsTargetCompleted(PreliminaryTargetId)
                ? 1
                : 0;

        private GameplayOutcomeReplayIdentity RequirePreliminaryOutcome(
            bool throwOnFailure)
        {
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.OffenseExpeditionResult);
            string ownerIdentity =
                "offense-expedition-result:" + PreliminaryExpeditionId;
            string ownerKey = definition.ProducerId + "|" + ownerIdentity;
            string operationHash = NarrativeInferenceHash.ComputeSha256Utf8(
                ownerKey).Substring(0, 32);
            var key = new GameplayResultKey(
                definition.ProducerId,
                new GameplayOperationId(
                    "migrated:"
                    + (int)MigratedProducerOutcomeKind.OffenseExpeditionResult
                    + ":"
                    + operationHash
                    + ":1"),
                1L,
                0);
            return RequireOutcome(
                key,
                definition.OutcomeTypeId,
                expectedTypeCount: 1,
                throwOnFailure);
        }

        private GameplayOutcomeReplayIdentity RequireTruthOutcome(
            bool throwOnFailure)
        {
            var key = new GameplayResultKey(
                ExternalFactionOutcomeIds.TruthRevealProducerId,
                new GameplayOperationId(
                    "offense-truth-reveal:" + TruthTargetId),
                2L,
                0);
            return RequireOutcome(
                key,
                ExternalFactionOutcomeIds.TruthRevealed,
                expectedTypeCount: 1,
                throwOnFailure);
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(
            GameplayResultKey key,
            GameplayOutcomeTypeId outcomeTypeId,
            int expectedTypeCount,
            bool throwOnFailure)
        {
            GameplayOutcomeReplayIdentity identity = default;
            bool valid = OutcomeCount(outcomeTypeId) == expectedTypeCount
                && ledger.TryGetResultIdentity(key, out identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact != null
                && string.Equals(
                    exact.outcomeTypeId,
                    outcomeTypeId.Value,
                    StringComparison.Ordinal)
                && exact.ownerRevision == key.CommitRevision;
            if (!valid && throwOnFailure)
            {
                throw new InvalidOperationException(
                    "Causal offense truth production entry did not publish its exact outcome: "
                    + outcomeTypeId.Value);
            }
            return valid ? identity : default;
        }

        private int ExactOutcomeCount() =>
            ledger.CaptureGameplayOutcomes().exactOutcomes.Count;

        private int OutcomeCount(GameplayOutcomeTypeId typeId) =>
            ledger.CaptureGameplayOutcomes().exactOutcomes.Count(value =>
                value != null
                && string.Equals(
                    value.outcomeTypeId,
                    typeId.Value,
                    StringComparison.Ordinal));

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                ledger);
            RequireFields(
                state,
                "offense truth-reveal owner state",
                "schemaVersion",
                "schema",
                "preliminaryTargetId",
                "truthTargetId",
                "stage",
                "campaign",
                "migratedOutcomeOwners");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals(
                    (string)state["schema"],
                    DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["preliminaryTargetId"],
                    PreliminaryTargetId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["truthTargetId"],
                    TruthTargetId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Offense truth-reveal checkpoint identity changed.");
            }

            DungeonMigratedProducerOutcomeSaveData migrated =
                state["migratedOutcomeOwners"]
                    .ToObject<DungeonMigratedProducerOutcomeSaveData>()
                ?? throw new InvalidOperationException(
                    "Offense truth migrated outcome owner state is missing.");
            migratedOutcomes.Restore(migratedOutcomes.PrepareRestore(migrated));
            DungeonOffenseCampaignSaveData campaignState = state["campaign"]
                    .ToObject<DungeonOffenseCampaignSaveData>()
                ?? throw new InvalidOperationException(
                    "Offense truth campaign state is missing.");
            campaign.PublishRestoreCandidate(
                campaign.BuildRestoreCandidate(campaignState));
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Offense truth checkpoint stage disagrees with campaign state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            DungeonMigratedProducerOutcomeSaveData migrated =
                migratedOutcomes.Capture();
            bool preliminary = campaign.State.IsTargetCompleted(
                PreliminaryTargetId);
            bool truth = campaign.State.IsTargetCompleted(TruthTargetId);
            bool valid = stage is >= 0 and <= 2
                && calendar.Day == AbsoluteDay
                && campaign.State.KnownTargetIds.Count == 2
                && campaign.State.KnowTarget(PreliminaryTargetId)
                && campaign.State.KnowTarget(TruthTargetId)
                && preliminary == (stage >= 1)
                && truth == (stage == 2)
                && campaign.State.TruthRevealed == (stage == 2)
                && string.Equals(
                    campaign.State.RevealedTruthTargetId,
                    stage == 2 ? TruthTargetId : string.Empty,
                    StringComparison.Ordinal)
                && campaign.State.CompletedTargetCount == stage
                && ExactOutcomeCount() == stage
                && OutcomeCount(PreliminaryOutcomeTypeId)
                    == (stage >= 1 ? 1 : 0)
                && OutcomeCount(ExternalFactionOutcomeIds.TruthRevealed)
                    == (stage == 2 ? 1 : 0)
                && migrated.owners.Count == (stage >= 1 ? 1 : 0);
            if (stage >= 1)
            {
                valid &= migrated.owners.Count == 1;
                if (migrated.owners.Count == 1)
                {
                    valid &= migrated.owners[0].revision == 1L
                        && RequirePreliminaryOutcome(throwOnFailure: false)
                            .HasCanonicalPayloadHash;
                }
            }
            if (stage == 2)
            {
                valid &= RequireTruthOutcome(throwOnFailure: false)
                    .HasCanonicalPayloadHash;
            }
            if (!valid)
            {
                throw new InvalidOperationException(
                    "Offense truth-reveal campaign, owner revision and ledger state diverged.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private sealed class FixedCampaignCatalog : IOffenseCampaignCatalog
    {
        private readonly IReadOnlyList<OffenseTargetDefinition> targets;

        internal FixedCampaignCatalog(
            IEnumerable<OffenseTargetDefinition> definitions) =>
            targets = definitions
                .Select(value => value?.CreateRuntimeCopy()
                    ?? throw new ArgumentException(
                        "Offense truth catalog contains a null target.",
                        nameof(definitions)))
                .ToArray();

        public IReadOnlyList<OffenseCampaignRegionDefinition> Regions => OffenseCampaignCatalogSO.CreateDefaultRegions();
        public IReadOnlyList<OffenseTargetDefinition> Targets => targets;

        public bool TryGet(
            string targetId,
            out OffenseTargetDefinition definition)
        {
            definition = targets.FirstOrDefault(value => string.Equals(
                value.id,
                targetId,
                StringComparison.Ordinal));
            return definition != null;
        }
    }

    private sealed class EmptyRewardContextBuilder :
        IOffenseRewardContextBuilder
    {
        internal static readonly EmptyRewardContextBuilder Instance = new();

        public OffenseRewardContext Create(
            OffenseTargetDefinition target,
            OffenseRewardState state,
            OffenseRewardDebugContext debugContext,
            string expeditionId = "") => new()
        {
            target = target,
            rewardState = state,
            expeditionId = expeditionId ?? string.Empty
        };
    }

    private sealed class EmptyRewardGrantService : IOffenseRewardGrantService
    {
        internal static readonly EmptyRewardGrantService Instance = new();

        public IReadOnlyList<OffenseRewardGrantResult> GrantRewards(
            IEnumerable<OffenseRewardPreview> rewards,
            OffenseRewardContext context) =>
            Array.Empty<OffenseRewardGrantResult>();
    }

    private sealed class EmptyMetaUpgradeCatalog :
        IMetaUpgradeDefinitionCatalog
    {
        internal static readonly EmptyMetaUpgradeCatalog Instance = new();

        public IReadOnlyCollection<MetaUpgradeDefinition> All =>
            Array.Empty<MetaUpgradeDefinition>();

        public MetaUpgradeDefinition Get(string id) => null;

        public MetaUpgradeDefinition Require(string id) =>
            throw new KeyNotFoundException(id);
    }

    private sealed class EmptyMetaApplicationPort :
        IMetaRuntimeApplicationPort
    {
        internal static readonly EmptyMetaApplicationPort Instance = new();

        public void Bind(IMetaRuntimeEventSink runtime) { }

        public void Unbind(IMetaRuntimeEventSink runtime) { }

        public MetaRunEnvironmentSnapshot CaptureRunEnvironment() => new(
            1f,
            DungeonDifficulty.Normal,
            DungeonSurvivalPressure.Standard);

        public void PublishUpgradePurchased(
            MetaUpgradePurchasedEvent purchasedEvent,
            string message) { }

        public void PublishRunResult(RunResultReadyEvent readyEvent) { }

        public void ShowRunResult(RunResultSnapshot result) { }
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        internal FixedCalendar(int day, int hour)
        {
            Day = Math.Max(1, day);
            Hour = Math.Clamp(hour, 0, 23);
        }

        public int Day { get; private set; }
        public int Hour { get; private set; }
        public int Year => 1;
        public int DayOfYear => Day;
        public Season Season => default;
        public int DayOfSeason => Day;
        public long AbsoluteHour => ((long)Day * 24L) + Hour;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => default;
        public bool IsRunning => true;
        public CalendarDateTime Current => default;
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) => default;
        public void Start() { }

        public void SetDateTime(int day, int hour)
        {
            Day = Math.Max(1, day);
            Hour = Math.Clamp(hour, 0, 23);
        }
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal FixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures
                ?? throw new ArgumentNullException(nameof(fixtures));

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

    private static IReadOnlyList<OffenseTargetDefinition> CreateTargets() =>
        new[]
        {
            new OffenseTargetDefinition
            {
                id = PreliminaryTargetId,
                title = "봉인된 외곽 기록소",
                description = "왕도 심부로 이어지는 선행 기록을 확보하는 원정 목표다.",
                kind = OffenseTargetKind.ResourceSite,
                campaignOrder = 1,
                distance = 0f,
                danger = 4f,
                durationSeconds = 18f,
                requiredMembers = 1,
                requiredPower = 10f,
                rewards = Array.Empty<OffenseRewardPreview>()
            },
            new OffenseTargetDefinition
            {
                id = TruthTargetId,
                title = "매몰된 왕도 심부",
                description = "외곽 기록을 해독한 뒤에만 진입할 수 있는 최종 목표다.",
                kind = OffenseTargetKind.SpecialEvent,
                campaignOrder = 2,
                prerequisiteTargetId = PreliminaryTargetId,
                revealsTruth = true,
                truthText = "지상의 왕국이 던전을 유배지와 채굴장으로 만들었다는 기록이 드러났다.",
                distance = 0f,
                danger = 9f,
                durationSeconds = 30f,
                requiredMembers = 1,
                requiredPower = 18f,
                rewards = Array.Empty<OffenseRewardPreview>()
            }
        };

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
        string[] wanted = expected.OrderBy(
            name => name,
            StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
