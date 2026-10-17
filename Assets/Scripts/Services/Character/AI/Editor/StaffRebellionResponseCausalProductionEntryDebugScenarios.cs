#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class StaffRebellionResponseDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:staff-rebellion-response";
    internal const string CausalIsolateActionId =
        "action:social-staff-rebel-isolate";
    internal const string CausalCalmActionId =
        "action:social-staff-discontent-calm";

    private const string CausalOwnerKind =
        "owner-kind:staff-rebellion-response";
    private const string CausalStateSchema =
        "dungeonstory-causal-staff-rebellion-owner-state@1";

    internal static IReadOnlyList<IProductionCausalActionFixture>
        CreateCausalFixtures(
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
                "A staff-response checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new CausalStaffResponseContext(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                CausalIsolateActionId,
                "relationship",
                StaffDiscontentOutcomeIds.ResponseResolved,
                context.CaptureState,
                context.IsolateRebel,
                context.RequireIsolationOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.OwnerId,
                context.OwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                CausalCalmActionId,
                "relationship",
                StaffDiscontentOutcomeIds.ResponseResolved,
                () => new Dictionary<string, object>(),
                context.CalmWorker,
                context.RequireCalmOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.OwnerId,
                context.OwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string ownerId = "settlement:causal-staff-response:focused";
        JObject isolatedCheckpoint;
        using (var fixtures = new CausalFixtureSet(CreateCausalFixtures(
                   "run:causal-staff-response:isolate",
                   ownerId)))
        {
            IProductionCausalActionFixture isolate = fixtures.Get(
                CausalIsolateActionId);
            IReadOnlyDictionary<string, object> before = isolate.CaptureState();
            RequireCausal(
                (int)before["staffResponse.stage"] == 0
                && (long)before["staffResponse.outcomeRevision"] == 0L
                && (string)before["staffResponse.rebelStage"]
                    == StaffDiscontentStage.LocalRebellion.ToString()
                && !(bool)before["staffResponse.rebelIsolated"]
                && (string)before["staffResponse.workerStage"]
                    == StaffDiscontentStage.WorkDisruption.ToString(),
                "Staff-response owner did not start from the expected live state.");
            RequireCausal(isolate.Execute(),
                "The real isolation command rejected the active rebel.");
            RequireCausal(
                isolate.RequireExactOutcome().HasCanonicalPayloadHash,
                "The isolation response has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = isolate.CaptureState();
            RequireCausal(
                (int)after["staffResponse.stage"] == 1
                && (long)after["staffResponse.outcomeRevision"] == 1L
                && (bool)after["staffResponse.rebelIsolated"]
                && (string)after["staffResponse.workerStage"]
                    == StaffDiscontentStage.WorkDisruption.ToString()
                && (int)after["staffResponse.exactOutcomeCount"] == 1,
                "Isolation did not retain the staff-discontent owner state.");
            isolatedCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)isolate)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new CausalFixtureSet(CreateCausalFixtures(
                   "run:causal-staff-response:calm",
                   ownerId,
                   isolatedCheckpoint)))
        {
            IProductionCausalActionFixture isolate = fixtures.Get(
                CausalIsolateActionId);
            IProductionCausalActionFixture calm = fixtures.Get(
                CausalCalmActionId);
            IReadOnlyDictionary<string, object> restored =
                isolate.CaptureState();
            RequireCausal(
                (int)restored["staffResponse.stage"] == 1
                && (bool)restored["staffResponse.rebelIsolated"]
                && !isolate.Execute(),
                "Fresh composition restore lost or replayed isolation.");
            RequireCausal(calm.Execute(),
                "The real calming command rejected the disruptive worker.");
            RequireCausal(
                calm.RequireExactOutcome().HasCanonicalPayloadHash,
                "The calming response has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = isolate.CaptureState();
            RequireCausal(
                (int)after["staffResponse.stage"] == 2
                && (long)after["staffResponse.outcomeRevision"] == 2L
                && (bool)after["staffResponse.rebelIsolated"]
                && (string)after["staffResponse.workerStage"]
                    != StaffDiscontentStage.WorkDisruption.ToString()
                && (float)after["staffResponse.workerMood"] > 20f
                && (int)after["staffResponse.exactOutcomeCount"] == 2,
                "Calming did not preserve isolation and resolve disruption.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)calm)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new CausalFixtureSet(CreateCausalFixtures(
                   "run:causal-staff-response:terminal",
                   ownerId,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture isolate = fixtures.Get(
                CausalIsolateActionId);
            IProductionCausalActionFixture calm = fixtures.Get(
                CausalCalmActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)isolate)
                .CaptureOwnerCheckpoint();
            RequireCausal(!isolate.Execute() && !calm.Execute(),
                "Terminal staff-response owner was not idempotent.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)isolate)
                .CaptureOwnerCheckpoint();
            RequireCausal(JToken.DeepEquals(before, after),
                "Rejected staff-response replay mutated the owner checkpoint.");
        }

        JObject tampered = (JObject)isolatedCheckpoint.DeepClone();
        ((JObject)tampered["domainState"])["workerId"] =
            "character:causal-staff-response:tampered";
        bool rejected = false;
        try
        {
            using var ignored = new CausalFixtureSet(CreateCausalFixtures(
                "run:causal-staff-response:tampered",
                ownerId,
                tampered));
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        RequireCausal(rejected,
            "A tampered staff identity was accepted across restore.");

        if (log)
            Debug.Log("Staff isolation -> calming causal owner family PASS");
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            RequireCausal(RunCausalOwnerFamily(log: true),
                "Staff-response causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private sealed class CausalStaffResponseContext : IDisposable
    {
        private readonly string ownerId;
        private readonly string rebelId;
        private readonly string workerId;
        private readonly string responderId;
        private readonly RebellionScenarioWorld world;
        private readonly CharacterActor rebel;
        private readonly CharacterActor worker;
        private readonly CharacterActor responder;
        private bool isolationExecuted;
        private bool calmExecuted;
        private bool disposed;

        internal CausalStaffResponseContext(
            string causalRunId,
            string ownerId,
            JObject ownerCheckpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string stableSeed = HasPersistentOwner ? this.ownerId : causalRunId;
            string scope = ProductionCausalOutcomeEvidence
                .Sha256Text(stableSeed)
                .Substring("sha256:".Length, 24);
            rebelId = "character:causal-staff-rebel:" + scope;
            workerId = "character:causal-staff-worker:" + scope;
            responderId = "character:causal-staff-responder:" + scope;
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    CausalOwnerKind)
                : causalRunId + ":staff-response-ledger";
            world = new RebellionScenarioWorld(
                ledgerRunId: ledgerRunId);
            rebel = world.CreateStaff(
                7311,
                "반란 직원 서린",
                new Vector2Int(2, 0),
                5f,
                rebelId);
            worker = world.CreateStaff(
                7312,
                "태업 직원 다온",
                new Vector2Int(4, 0),
                20f,
                workerId);
            responder = world.CreateStaff(
                7313,
                "관리자 연화",
                new Vector2Int(0, 0),
                80f,
                responderId);

            try
            {
                if (ownerCheckpoint == null)
                    PrepareInitialState();
                else
                    RestoreOwnerCheckpoint(ownerCheckpoint);
                ValidateState();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string OwnerId => ownerId;
        internal string OwnerKind => HasPersistentOwner
            ? CausalOwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            RequireRecords(
                out StaffDiscontentRecord rebelRecord,
                out StaffDiscontentRecord workerRecord);
            return new Dictionary<string, object>
            {
                ["staffResponse.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "settlement:ephemeral-staff-response",
                ["staffResponse.rebelId"] = rebelId,
                ["staffResponse.workerId"] = workerId,
                ["staffResponse.responderId"] = responderId,
                ["staffResponse.stage"] = Stage,
                ["staffResponse.outcomeRevision"] = world.Runtime.OutcomeRevision,
                ["staffResponse.rebelStage"] = rebelRecord.Stage.ToString(),
                ["staffResponse.rebelIsolated"] = rebelRecord.IsIsolated,
                ["staffResponse.workerStage"] = workerRecord.Stage.ToString(),
                ["staffResponse.workerMood"] = worker.stats[CharacterCondition.MOOD],
                ["staffResponse.workerLowMoodDays"] = workerRecord.LowMoodDays,
                ["staffResponse.exactOutcomeCount"] = ExactOutcomeCount(),
                ["staffResponse.isolationExecuted"] = isolationExecuted,
                ["staffResponse.calmExecuted"] = calmExecuted
            };
        }

        internal bool IsolateRebel()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            if (!world.Runtime.TryIsolateRebel(
                    rebel,
                    responder,
                    out StaffRebellionResponseResult result)
                || !result.Success)
            {
                return false;
            }
            isolationExecuted = true;
            ValidateState();
            return RequireOutcome(1L, throwOnFailure: false)
                .HasCanonicalPayloadHash;
        }

        internal bool CalmWorker()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            if (!world.Runtime.TryCalmStaff(
                    worker,
                    responder,
                    out StaffRebellionResponseResult result)
                || !result.Success)
            {
                return false;
            }
            calmExecuted = true;
            ValidateState();
            return RequireOutcome(2L, throwOnFailure: false)
                .HasCanonicalPayloadHash;
        }

        internal GameplayOutcomeReplayIdentity RequireIsolationOutcome() =>
            RequireOutcome(1L, throwOnFailure: true);

        internal GameplayOutcomeReplayIdentity RequireCalmOutcome() =>
            RequireOutcome(2L, throwOnFailure: true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(world.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral staff-response fixtures have no checkpoint.");
            }
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                CausalOwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = CausalStateSchema,
                    ["rebelId"] = rebelId,
                    ["workerId"] = workerId,
                    ["responderId"] = responderId,
                    ["stage"] = Stage,
                    ["staffDiscontent"] = JObject.Parse(
                        CreateSaveSection().Capture()),
                    ["rebelStats"] = CaptureStats(rebel),
                    ["workerStats"] = CaptureStats(worker),
                    ["responderStats"] = CaptureStats(responder),
                    ["isolationExecuted"] = isolationExecuted,
                    ["calmExecuted"] = calmExecuted
                },
                world.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            world?.Dispose();
        }

        private int Stage => calmExecuted ? 2 : isolationExecuted ? 1 : 0;

        private void PrepareInitialState()
        {
            StaffDiscontentRecord rebelRecord = world.Runtime.ProcessStaff(
                rebel,
                out StaffDiscontentOutcome rebelOutcome);
            StaffDiscontentRecord workerRecord = world.Runtime.ProcessStaff(
                worker,
                out StaffDiscontentOutcome workerOutcome);
            if (rebelRecord == null
                || rebelOutcome != StaffDiscontentOutcome.LocalRebellion
                || workerRecord == null
                || workerOutcome != StaffDiscontentOutcome.WorkDisruption)
            {
                throw new InvalidOperationException(
                    "Could not establish the live staff-discontent preconditions.");
            }
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                CausalOwnerKind,
                CausalOwnerRestoreKind,
                world.Ledger);
            RequireFields(
                state,
                "causal staff-response owner state",
                "schemaVersion",
                "schema",
                "rebelId",
                "workerId",
                "responderId",
                "stage",
                "staffDiscontent",
                "rebelStats",
                "workerStats",
                "responderStats",
                "isolationExecuted",
                "calmExecuted");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals(
                    (string)state["schema"],
                    CausalStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["rebelId"], rebelId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["workerId"], workerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["responderId"], responderId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Causal staff-response identities changed.");
            }

            RestoreStats(rebel, state["rebelStats"]);
            RestoreStats(worker, state["workerStats"]);
            RestoreStats(responder, state["responderStats"]);
            StaffDiscontentSaveSection section = CreateSaveSection();
            DungeonGameRestoreReport report = new();
            section.Restore(
                state["staffDiscontent"].ToString(Formatting.None),
                section.SectionVersion,
                report);
            if (!report.Success)
            {
                throw new InvalidOperationException(
                    "Causal staff-discontent restore failed: "
                    + string.Join(" | ", report.Errors));
            }

            isolationExecuted = (bool)state["isolationExecuted"];
            calmExecuted = (bool)state["calmExecuted"];
            int savedStage = (int)state["stage"];
            if (savedStage != Stage)
            {
                throw new InvalidOperationException(
                    "Causal staff-response stage disagrees with action history.");
            }
        }

        private void ValidateState()
        {
            RequireRecords(
                out StaffDiscontentRecord rebelRecord,
                out StaffDiscontentRecord workerRecord);
            if (calmExecuted && !isolationExecuted)
            {
                throw new InvalidOperationException(
                    "Calming cannot precede the isolation stage.");
            }
            if (world.Runtime.OutcomeRevision != Stage
                || ExactOutcomeCount() != Stage
                || rebelRecord.Stage != StaffDiscontentStage.LocalRebellion
                || rebelRecord.IsIsolated != isolationExecuted
                || !rebelRecord.IsPermanentLoss
                || !rebelRecord.IsInLocalRebellion
                || workerRecord.IsIsolated
                || workerRecord.IsPermanentLoss
                || (!calmExecuted
                    && workerRecord.Stage
                        != StaffDiscontentStage.WorkDisruption)
                || (calmExecuted
                    && workerRecord.Stage
                        is StaffDiscontentStage.WorkDisruption
                            or StaffDiscontentStage.LocalRebellion)
                || (!calmExecuted
                    && !Mathf.Approximately(
                        worker.stats[CharacterCondition.MOOD], 20f))
                || (calmExecuted
                    && worker.stats[CharacterCondition.MOOD] <= 20f))
            {
                throw new InvalidOperationException(
                    "Causal staff-response domain, actor, and ledger state diverged.");
            }
            if (isolationExecuted)
                _ = RequireOutcome(1L, throwOnFailure: true);
            if (calmExecuted)
                _ = RequireOutcome(2L, throwOnFailure: true);
        }

        private void RequireRecords(
            out StaffDiscontentRecord rebelRecord,
            out StaffDiscontentRecord workerRecord)
        {
            if (!world.Runtime.State.TryGetRecord(rebel, out rebelRecord)
                || !world.Runtime.State.TryGetRecord(worker, out workerRecord)
                || world.Runtime.State.Records.Count != 2)
            {
                throw new InvalidOperationException(
                    "Causal staff-response records are missing or duplicated.");
            }
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(
            long revision,
            bool throwOnFailure)
        {
            GameplayResultKey key = StaffDiscontentOutcomeIds.ResultKey(revision);
            bool valid = world.Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && world.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact != null
                && string.Equals(
                    exact.producerId,
                    StaffDiscontentOutcomeIds.ProducerId,
                    StringComparison.Ordinal)
                && string.Equals(
                    exact.outcomeTypeId,
                    StaffDiscontentOutcomeIds.ResponseResolved.Value,
                    StringComparison.Ordinal)
                && exact.ownerRevision == revision
                && exact.commitRevision == revision
                && string.Equals(
                    exact.operationId,
                    "staff-response:" + revision,
                    StringComparison.Ordinal);
            if (!valid && throwOnFailure)
            {
                throw new InvalidOperationException(
                    "Causal staff response did not publish exact revision "
                    + revision + ".");
            }
            return valid ? identity : default;
        }

        private int ExactOutcomeCount() => world.Ledger.GetGlobal(
            OutcomeCursor.FirstPage(16), OutcomeFilter.All).Items.Count;

        private StaffDiscontentSaveSection CreateSaveSection() => new(
            new CharacterSceneRuntimeReferences(
                null,
                null,
                world.Runtime,
                null,
                null,
                null,
                null,
                null));

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private sealed class CausalFixtureSet : IDisposable
    {
        private readonly Dictionary<string, IProductionCausalActionFixture>
            fixtures;

        internal CausalFixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> source)
        {
            fixtures = (source
                    ?? throw new ArgumentNullException(nameof(source)))
                .ToDictionary(value => value.ActionId, StringComparer.Ordinal);
        }

        internal IProductionCausalActionFixture Get(string actionId) =>
            fixtures.TryGetValue(actionId, out IProductionCausalActionFixture value)
                ? value
                : throw new InvalidOperationException(
                    "Missing staff-response causal fixture: " + actionId);

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures.Values)
                fixture.Dispose();
        }
    }

    private static JObject CaptureStats(CharacterActor actor)
    {
        CharacterMoodSnapshot mood = actor?.Mood
            ?? throw new InvalidOperationException(
                "Causal staff-response actor mood is unavailable.");
        return new JObject
        {
            ["conditions"] = new JArray(actor.Stats.StatSnapshot
                .OrderBy(value => (int)value.Key)
                .Select(value => new JObject
                {
                    ["condition"] = value.Key.ToString(),
                    ["value"] = value.Value
                })),
            ["currentHealth"] = actor.Stats.CurrentHealth,
            ["injurySeverity"] = actor.Stats.InjurySeverity,
            ["baseMood"] = mood.BaseValue,
            ["moodFactors"] = new JArray(mood.Factors
                .Where(value => value != null
                    && value.Kind == CharacterMoodFactorKind.Interaction)
                .OrderBy(value => value.Id, StringComparer.Ordinal)
                .Select(value => new JObject
                {
                    ["id"] = value.Id,
                    ["label"] = value.Label,
                    ["value"] = value.Value,
                    ["remainingSeconds"] = value.RemainingSeconds
                }))
        };
    }

    private static void RestoreStats(CharacterActor actor, JToken token)
    {
        if (actor == null || token is not JObject value)
            throw new InvalidOperationException(
                "Causal staff-response actor stats are missing.");
        RequireFields(
            value,
            "causal staff-response actor stats",
            "conditions",
            "currentHealth",
            "injurySeverity",
            "baseMood",
            "moodFactors");
        if (value["conditions"] is not JArray conditions
            || value["moodFactors"] is not JArray factors)
        {
            throw new InvalidOperationException(
                "Causal staff-response stat arrays are invalid.");
        }

        var restoredStats = new Dictionary<CharacterCondition, float>();
        foreach (JObject row in conditions.Cast<JObject>())
        {
            RequireFields(
                row,
                "causal staff-response condition",
                "condition",
                "value");
            if (!Enum.TryParse(
                    (string)row["condition"],
                    ignoreCase: false,
                    out CharacterCondition condition)
                || !restoredStats.TryAdd(condition, (float)row["value"]))
            {
                throw new InvalidOperationException(
                    "Causal staff-response condition is unknown or duplicated.");
            }
        }

        var restoredFactors = new List<CharacterMoodFactorSnapshot>();
        string previousFactor = string.Empty;
        foreach (JObject row in factors.Cast<JObject>())
        {
            RequireFields(
                row,
                "causal staff-response mood factor",
                "id",
                "label",
                "value",
                "remainingSeconds");
            string id = (string)row["id"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id)
                || previousFactor.Length > 0
                && string.CompareOrdinal(previousFactor, id) >= 0)
            {
                throw new InvalidOperationException(
                    "Causal staff-response mood factors are invalid or unordered.");
            }
            previousFactor = id;
            restoredFactors.Add(new CharacterMoodFactorSnapshot(
                id,
                (string)row["label"] ?? string.Empty,
                (float)row["value"],
                CharacterMoodFactorKind.Interaction,
                (float)row["remainingSeconds"]));
        }
        actor.Stats.RestorePersistentState(
            restoredStats,
            (float)value["currentHealth"],
            (float)value["injurySeverity"],
            (float)value["baseMood"],
            restoredFactors);
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] fields)
    {
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expected = fields
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", expected));
        }
    }

    private static void RequireCausal(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
