#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class KnowledgeResearchOutcomeDebugScenarios
{
    internal const string CausalResearchOwnerRestoreKind =
        "owner-family:research-knowledge-progress";
    internal const string CausalResearchProgressActionId =
        "action:research-knowledge-progress";
    internal const string CausalResearchCompletionActionId =
        "action:research-knowledge-completion";

    private const string CausalResearchOwnerKind =
        "owner-kind:research-knowledge-progress";
    private const string CausalResearchStateSchema =
        "dungeonstory-causal-research-knowledge-owner-state@1";

    internal static IReadOnlyList<IProductionCausalActionFixture>
        CreateCausalResearchFixtures(
            string causalRunId,
            string ownerId = null,
            JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException("A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A research checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new CausalResearchContext(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                CausalResearchProgressActionId,
                "research",
                ResearchWorkOutcomeIds.WorkApplied,
                context.CaptureState,
                context.ApplyProgress,
                context.RequireProgressOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalResearchOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                CausalResearchCompletionActionId,
                "research",
                ResearchWorkOutcomeIds.WorkApplied,
                () => new Dictionary<string, object>(),
                context.CompleteResearch,
                context.RequireCompletionOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalResearchOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    public static bool RunCausalResearchOwnerFamily(bool log = false)
    {
        const string OwnerIdValue = "owner:causal-research-knowledge:focused";
        JObject progressCheckpoint;
        using (var fixtures = new CausalResearchFixtureSet(
                   CreateCausalResearchFixtures(
                       "run:causal-research-knowledge:progress",
                       OwnerIdValue)))
        {
            IProductionCausalActionFixture progress = fixtures.Get(
                CausalResearchProgressActionId);
            IReadOnlyDictionary<string, object> before = progress.CaptureState();
            Require((float)before["research.completedWork"] == 0f
                    && (long)before["research.outcomeSequence"] == 0L
                    && (bool)before["research.sourceAvailable"],
                "The research family did not start from an untouched knowledge task.");
            Require(progress.Execute(),
                "The real knowledge-research work entry did not apply partial work.");
            Require(progress.RequireExactOutcome().HasCanonicalPayloadHash,
                "Partial research work has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = progress.CaptureState();
            Require((float)after["research.completedWork"] == 8f
                    && (long)after["research.outcomeSequence"] == 1L
                    && !(bool)after["research.completed"]
                    && (bool)after["research.sourceAvailable"],
                "Partial research work did not preserve the exact task and source.");
            progressCheckpoint = ((IProductionCausalOwnerCheckpointFixture)progress)
                .CaptureOwnerCheckpoint();
        }

        JObject completedCheckpoint;
        using (var fixtures = new CausalResearchFixtureSet(
                   CreateCausalResearchFixtures(
                       "run:causal-research-knowledge:completion",
                       OwnerIdValue,
                       progressCheckpoint)))
        {
            IProductionCausalActionFixture completion = fixtures.Get(
                CausalResearchCompletionActionId);
            IReadOnlyDictionary<string, object> before = fixtures
                .Get(CausalResearchProgressActionId)
                .CaptureState();
            Require((float)before["research.completedWork"] == 8f
                    && !(bool)before["research.completed"]
                    && (bool)before["research.sourceAvailable"],
                "Fresh restore lost partial research progress or its physical source.");
            Require(completion.Execute(),
                "The real knowledge-research work entry did not finish the task.");
            Require(completion.RequireExactOutcome().HasCanonicalPayloadHash,
                "Terminal research work has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = fixtures
                .Get(CausalResearchProgressActionId)
                .CaptureState();
            Require((float)after["research.completedWork"] == 24f
                    && (long)after["research.outcomeSequence"] == 2L
                    && (bool)after["research.completed"]
                    && !(bool)after["research.sourceAvailable"]
                    && (float)after["research.intelligenceDamage"] == 10f,
                "Terminal research did not consume the source and apply the real reward.");
            completedCheckpoint = ((IProductionCausalOwnerCheckpointFixture)completion)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new CausalResearchFixtureSet(
                   CreateCausalResearchFixtures(
                       "run:causal-research-knowledge:terminal-restore",
                       OwnerIdValue,
                       completedCheckpoint)))
        {
            IReadOnlyDictionary<string, object> restored = fixtures
                .Get(CausalResearchProgressActionId)
                .CaptureState();
            Require((bool)restored["research.completed"]
                    && (float)restored["research.completedWork"] == 24f
                    && !(bool)restored["research.sourceAvailable"]
                    && (long)restored["research.outcomeSequence"] == 2L,
                "Completed research did not survive a second fresh restore.");
            Require(!fixtures.Get(CausalResearchProgressActionId).Execute()
                    && !fixtures.Get(CausalResearchCompletionActionId).Execute(),
                "A completed research task accepted duplicate work after restore.");
        }

        JObject tampered = (JObject)completedCheckpoint.DeepClone();
        ((JObject)tampered["domainState"])["sourceStackId"] =
            "stack:causal-research:tampered";
        bool tamperRejected = false;
        try
        {
            using var ignored = new CausalResearchFixtureSet(
                CreateCausalResearchFixtures(
                    "run:causal-research-knowledge:tampered",
                    OwnerIdValue,
                    tampered));
        }
        catch (InvalidOperationException)
        {
            tamperRejected = true;
        }
        Require(tamperRejected,
            "A research checkpoint with a different physical source was accepted.");

        if (log)
            Debug.Log("Research knowledge progress -> completion causal owner family PASS");
        return true;
    }

    private sealed class CausalResearchContext : IDisposable
    {
        private readonly string ownerId;
        private readonly string facilityId;
        private readonly string expectedSourceStackId;
        private readonly Fixture fixture;
        private bool progressApplied;
        private bool completionApplied;
        private bool disposed;

        internal CausalResearchContext(
            string causalRunId,
            string ownerId,
            JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string stableSeed = HasPersistentOwner ? this.ownerId : causalRunId;
            string scope = ProductionCausalOutcomeEvidence.Sha256Text(stableSeed)
                .Substring("sha256:".Length, 24);
            facilityId = "building:causal-research:" + scope;
            expectedSourceStackId =
                "stack:causal-research:" + scope + ":0001";
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    CausalResearchOwnerKind)
                : causalRunId + ":research-ledger";
            fixture = new Fixture(
                KnowledgeResidueUse.RegionReconnaissance,
                ledgerRunId,
                facilityId,
                new CausalResearchDeterministicIds(scope));
            try
            {
                if (checkpoint != null)
                    RestoreOwnerCheckpoint(checkpoint);
                ValidateState();
            }
            catch
            {
                fixture.Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string PersistentOwnerId => ownerId;
        internal string PersistentOwnerKind => HasPersistentOwner
            ? CausalResearchOwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            KnowledgeResidueTaskSaveData task = fixture.Task;
            return new Dictionary<string, object>
            {
                ["research.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-research-knowledge",
                ["research.facilityId"] = facilityId,
                ["research.taskId"] = Fixture.TaskId,
                ["research.sourceStackId"] = fixture.SourceStackId,
                ["research.completedWork"] = task?.completedWork ?? 24f,
                ["research.requiredWork"] = task?.requiredWork ?? 24f,
                ["research.intelligenceDamage"] =
                    fixture.Region.intelligenceDamage,
                ["research.outcomeSequence"] = fixture.Sequence,
                ["research.sourceAvailable"] = fixture.HasSource,
                ["research.progressApplied"] = progressApplied,
                ["research.completed"] = completionApplied
            };
        }

        internal bool ApplyProgress()
        {
            ThrowIfDisposed();
            if (progressApplied || completionApplied || fixture.Task == null
                || fixture.Task.completedWork != 0f)
            {
                return false;
            }
            BlueprintResearchWorkResult result = fixture.Runtime.ApplyApprovedWork(
                null,
                fixture.Facility,
                8f);
            progressApplied = result.Success
                && !result.Completed
                && result.TotalProgress == 8f
                && RequireOutcome(1L, false).HasCanonicalPayloadHash;
            ValidateState();
            return progressApplied;
        }

        internal bool CompleteResearch()
        {
            ThrowIfDisposed();
            if (!progressApplied || completionApplied || fixture.Task == null
                || fixture.Task.completedWork != 8f)
            {
                return false;
            }
            BlueprintResearchWorkResult result = fixture.Runtime.ApplyApprovedWork(
                null,
                fixture.Facility,
                16f);
            completionApplied = result.Success
                && result.Completed
                && result.TotalProgress == 24f
                && RequireOutcome(2L, false).HasCanonicalPayloadHash;
            ValidateState();
            return completionApplied;
        }

        internal GameplayOutcomeReplayIdentity RequireProgressOutcome() =>
            RequireOutcome(1L, true);

        internal GameplayOutcomeReplayIdentity RequireCompletionOutcome() =>
            RequireOutcome(2L, true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(fixture.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "This research context has no persistent owner.");
            }
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                CausalResearchOwnerKind,
                CausalResearchOwnerRestoreKind,
                SerializeCausalResearchState(
                    CaptureDomainState(),
                    "research knowledge state"),
                fixture.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            fixture.Dispose();
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(
            long revision,
            bool throwOnFailure)
        {
            var key = new GameplayResultKey(
                ResearchWorkOutcomeIds.ProducerId,
                new GameplayOperationId("research-work:" + revision),
                revision,
                0);
            bool valid = fixture.Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && fixture.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact != null
                && string.Equals(
                    exact.outcomeTypeId,
                    ResearchWorkOutcomeIds.WorkApplied.Value,
                    StringComparison.Ordinal);
            if (!valid && throwOnFailure)
            {
                throw new InvalidOperationException(
                    "Causal research work did not publish exact revision "
                    + revision + ".");
            }
            return valid ? identity : default;
        }

        private CausalResearchStateSaveData CaptureDomainState() => new()
        {
            schemaVersion = 1,
            schema = CausalResearchStateSchema,
            facilityId = facilityId,
            taskId = Fixture.TaskId,
            sourceStackId = fixture.SourceStackId,
            research = new DungeonResearchSaveData
            {
                outcomeSequence = fixture.Sequence,
                nextKnowledgeTaskSequence = fixture.Runtime.NextTaskSequence,
                knowledgeTaskIdentityGeneration =
                    fixture.Runtime.TaskIdentityGeneration,
                knowledgeTasks = fixture.Runtime.Capture().ToList()
            },
            physicalItems = fixture.Items.Capture(),
            intelligenceDamage = fixture.Region.intelligenceDamage,
            progressApplied = progressApplied,
            completionApplied = completionApplied
        };

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                CausalResearchOwnerKind,
                CausalResearchOwnerRestoreKind,
                fixture.Ledger);
            CausalResearchStateSaveData saved =
                DeserializeCausalResearchState<CausalResearchStateSaveData>(
                    state,
                    "research knowledge state");
            if (saved.schemaVersion != 1
                || !string.Equals(
                    saved.schema,
                    CausalResearchStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    saved.facilityId,
                    facilityId,
                    StringComparison.Ordinal)
                || !string.Equals(saved.taskId, Fixture.TaskId, StringComparison.Ordinal)
                || !string.Equals(
                    saved.sourceStackId,
                    expectedSourceStackId,
                    StringComparison.Ordinal)
                || saved.research == null
                || saved.physicalItems == null
                || string.IsNullOrWhiteSpace(saved.sourceStackId))
            {
                throw new InvalidOperationException(
                    "Causal research owner identity or state header changed.");
            }

            fixture.Root.Replace(new KnowledgeResidueAggregateState());
            new BlueprintResearchState(fixture.Root).RestoreOutcomeSequence(0L);
            fixture.Items.Restore(saved.physicalItems);
            BlueprintResearchSaveSection.ReconcileKnowledgeResiduePhysicalCandidate(
                saved.research.knowledgeTasks,
                new SavedPhysicalCandidateQuery(saved.physicalItems));
            fixture.Runtime.Restore(fixture.Runtime.PrepareRestore(
                saved.research.knowledgeTasks,
                saved.research.nextKnowledgeTaskSequence,
                saved.research.knowledgeTaskIdentityGeneration));
            new BlueprintResearchState(fixture.Root)
                .RestoreOutcomeSequence(saved.research.outcomeSequence);
            fixture.Region.intelligenceDamage = saved.intelligenceDamage;
            fixture.SourceStackId = saved.sourceStackId;
            progressApplied = saved.progressApplied;
            completionApplied = saved.completionApplied;
        }

        private void ValidateState()
        {
            KnowledgeResidueTaskSaveData task = fixture.Task;
            float completedWork = task?.completedWork ?? 24f;
            bool partialTruth = fixture.Sequence >= 1L && completedWork >= 8f;
            bool completionTruth = fixture.Sequence >= 2L
                && task == null
                && !fixture.HasSource
                && fixture.Region.intelligenceDamage == 10f;
            if (!string.Equals(
                    fixture.Facility.RequirePersistentInstanceId().Value,
                    facilityId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    fixture.SourceStackId,
                    expectedSourceStackId,
                    StringComparison.Ordinal)
                || progressApplied != partialTruth
                || completionApplied != completionTruth
                || fixture.Sequence is < 0L or > 2L
                || !completionApplied && task == null
                || task != null && (!string.Equals(
                        task.taskId,
                        Fixture.TaskId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        task.facilityInstanceId,
                        facilityId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        task.destinationId,
                        KnowledgeResidueDestinationAuthority.FormatDestinationId(
                            Fixture.TaskId,
                            1),
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Causal research flags disagree with task, physical, reward or ledger authority.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    [Serializable]
    private sealed class CausalResearchStateSaveData
    {
        public int schemaVersion;
        public string schema = string.Empty;
        public string facilityId = string.Empty;
        public string taskId = string.Empty;
        public string sourceStackId = string.Empty;
        public DungeonResearchSaveData research;
        public DungeonPhysicalItemSaveData physicalItems;
        public float intelligenceDamage;
        public bool progressApplied;
        public bool completionApplied;
    }

    private sealed class CausalResearchFixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal CausalResearchFixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures;

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

    private sealed class CausalResearchDeterministicIds : IPersistentIdGenerator
    {
        private readonly string scope;
        private int itemInstanceSequence;
        private int itemStackSequence;
        private int characterSequence;
        private int buildingSequence;
        private int wildlifeSequence;

        internal CausalResearchDeterministicIds(string scope) =>
            this.scope = scope;

        public ItemInstanceId NewItemInstanceId() => new(
            "item-instance:causal-research:" + scope + ":"
            + (++itemInstanceSequence).ToString("D4"));
        public ItemStackId NewItemStackId() => new(
            "stack:causal-research:" + scope + ":"
            + (++itemStackSequence).ToString("D4"));
        public CharacterId NewCharacterId() => new(
            "character:causal-research:" + scope + ":"
            + (++characterSequence).ToString("D4"));
        public BuildingInstanceId NewBuildingInstanceId() => new(
            "building:causal-research:" + scope + ":"
            + (++buildingSequence).ToString("D4"));
        public WildlifeHabitatPatchId NewWildlifeHabitatPatchId() => new(
            "wildlife-habitat:causal-research:" + scope + ":"
            + (++wildlifeSequence).ToString("D4"));
    }

    private static JObject SerializeCausalResearchState(object value, string label)
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

    private static T DeserializeCausalResearchState<T>(JToken value, string label)
        where T : class
    {
        if (value is not JObject json)
        {
            throw new InvalidOperationException(
                "Causal " + label + " checkpoint must be an object.");
        }
        T result = JsonUtility.FromJson<T>(json.ToString(Formatting.None));
        return result ?? throw new InvalidOperationException(
            "Causal " + label + " checkpoint deserialized to null.");
    }
}
#endif
