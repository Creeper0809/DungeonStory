#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

public static partial class EnvironmentCropProductionEntryDebugScenarios
{
    internal const string CertifiedSeedCausalOwnerRestoreKind =
        "owner-family:environment-certified-seed";
    internal const string CertifiedSeedFirstCompletionActionId =
        "action:environment-certified-seed-first-completion";
    internal const string CertifiedSeedRepeatCompletionActionId =
        "action:environment-certified-seed-repeat-completion";

    private const string CertifiedSeedCausalOwnerKind =
        "owner-kind:environment-certified-seed";
    private const string CertifiedSeedCausalStateSchema =
        "dungeonstory-causal-certified-seed-owner-state@1";

    internal static IReadOnlyList<IProductionCausalActionFixture>
        CreateCertifiedSeedCausalFixtures(
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
                "A certified-seed checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new CausalCertifiedSeedContext(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                CertifiedSeedFirstCompletionActionId,
                "environment",
                EnvironmentOutcomeIds.CertifiedSeedCompleted,
                context.CaptureState,
                context.CompleteFirstOrder,
                context.RequireFirstOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.OwnerId,
                context.OwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CertifiedSeedCausalOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                CertifiedSeedRepeatCompletionActionId,
                "environment",
                EnvironmentOutcomeIds.CertifiedSeedCompleted,
                () => new Dictionary<string, object>(),
                context.CompleteRepeatOrder,
                context.RequireRepeatOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.OwnerId,
                context.OwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CertifiedSeedCausalOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    internal static bool RunCertifiedSeedCausalOwnerFamily(bool log = false)
    {
        const string ownerId =
            "certified-seed-owner:twilight-grain:focused";
        JObject firstCheckpoint;
        using (var fixtures = new CertifiedSeedCausalFixtureSet(
                   CreateCertifiedSeedCausalFixtures(
                       "run:causal-certified-seed:first",
                       ownerId)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                CertifiedSeedFirstCompletionActionId);
            IReadOnlyDictionary<string, object> before = first.CaptureState();
            RequireCertifiedSeedCausal(
                (int)before["certifiedSeed.stage"] == 0
                && (int)before["certifiedSeed.nextOrderSequence"] == 0
                && (int)before["certifiedSeed.activeOrderCount"] == 0
                && (int)before["certifiedSeed.certifiedLotCount"] == 0
                && (int)before["certifiedSeed.exactOutcomeCount"] == 0,
                "Certified-seed owner did not start from the expected state.");
            RequireCertifiedSeedCausal(first.Execute(),
                "The first real certified-seed order did not complete.");
            RequireCertifiedSeedCausal(
                first.RequireExactOutcome().HasCanonicalPayloadHash,
                "The first certified-seed completion has no canonical hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            RequireCertifiedSeedCausal(
                (int)after["certifiedSeed.stage"] == 1
                && (int)after["certifiedSeed.nextOrderSequence"] == 1
                && (int)after["certifiedSeed.lastProcessedOperatingDay"] == 23
                && (int)after["certifiedSeed.activeOrderCount"] == 0
                && (int)after["certifiedSeed.certifiedLotCount"] == 1
                && (int)after["certifiedSeed.exactOutcomeCount"] == 1,
                "The first certified-seed completion did not retain its state.");
            firstCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new CertifiedSeedCausalFixtureSet(
                   CreateCertifiedSeedCausalFixtures(
                       "run:causal-certified-seed:repeat",
                       ownerId,
                       firstCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                CertifiedSeedFirstCompletionActionId);
            IProductionCausalActionFixture repeat = fixtures.Get(
                CertifiedSeedRepeatCompletionActionId);
            IReadOnlyDictionary<string, object> restored = first.CaptureState();
            RequireCertifiedSeedCausal(
                (int)restored["certifiedSeed.stage"] == 1
                && (int)restored["certifiedSeed.nextOrderSequence"] == 1
                && (int)restored["certifiedSeed.lastProcessedOperatingDay"] == 23
                && (int)restored["certifiedSeed.certifiedLotCount"] == 1
                && !first.Execute(),
                "Fresh restore lost or replayed the first certified-seed order.");
            RequireCertifiedSeedCausal(repeat.Execute(),
                "The restored owner did not complete its second real order.");
            RequireCertifiedSeedCausal(
                repeat.RequireExactOutcome().HasCanonicalPayloadHash,
                "The repeated certified-seed completion has no canonical hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            RequireCertifiedSeedCausal(
                (int)after["certifiedSeed.stage"] == 2
                && (int)after["certifiedSeed.nextOrderSequence"] == 2
                && (int)after["certifiedSeed.lastProcessedOperatingDay"] == 24
                && (int)after["certifiedSeed.operatingDay"] == 24
                && (int)after["certifiedSeed.activeOrderCount"] == 0
                && (int)after["certifiedSeed.certifiedLotCount"] == 2
                && (int)after["certifiedSeed.exactOutcomeCount"] == 2,
                "The second certified-seed completion did not consume restored state.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)repeat)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new CertifiedSeedCausalFixtureSet(
                   CreateCertifiedSeedCausalFixtures(
                       "run:causal-certified-seed:terminal",
                       ownerId,
                       terminalCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                CertifiedSeedFirstCompletionActionId);
            IProductionCausalActionFixture repeat = fixtures.Get(
                CertifiedSeedRepeatCompletionActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
            RequireCertifiedSeedCausal(!first.Execute() && !repeat.Execute(),
                "Terminal certified-seed owner was not idempotent.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)repeat)
                .CaptureOwnerCheckpoint();
            RequireCertifiedSeedCausal(JToken.DeepEquals(before, after),
                "Rejected certified-seed replay mutated the owner checkpoint.");
        }

        JObject facilityTamper = (JObject)firstCheckpoint.DeepClone();
        ((JObject)facilityTamper["domainState"])["facilityId"] =
            "building:tampered-certified-seed";
        RequireCertifiedSeedCausal(
            RejectCertifiedSeedCheckpoint(
                ownerId,
                facilityTamper,
                "facility-tamper"),
            "A tampered certified-seed facility identity was accepted.");

        JObject sequenceTamper = (JObject)firstCheckpoint.DeepClone();
        JObject sequenceState = (JObject)sequenceTamper["domainState"];
        ((JObject)sequenceState["certifiedSeedState"])["nextOrderSequence"] = 0;
        RequireCertifiedSeedCausal(
            RejectCertifiedSeedCheckpoint(
                ownerId,
                sequenceTamper,
                "sequence-tamper"),
            "A certified-seed stage/sequence mismatch was accepted.");

        if (log)
        {
            UnityEngine.Debug.Log(
                "Certified seed day-23 completion -> restored day-24 completion "
                + "causal owner family PASS");
        }
        return true;
    }

    public static void RunCertifiedSeedCausalBatch()
    {
        try
        {
            RequireCertifiedSeedCausal(
                RunCertifiedSeedCausalOwnerFamily(log: true),
                "Certified-seed causal owner family returned false.");
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
            throw;
        }
    }

    private static bool RejectCertifiedSeedCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new CertifiedSeedCausalFixtureSet(
                CreateCertifiedSeedCausalFixtures(
                    "run:causal-certified-seed:" + suffix,
                    ownerId,
                    checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class CausalCertifiedSeedContext : IDisposable
    {
        private readonly string ownerId;
        private readonly Fixture fixture;
        private readonly CertifiedSeedRuntime runtime;
        private readonly string facilityId;
        private bool firstExecuted;
        private bool repeatExecuted;
        private bool disposed;

        internal CausalCertifiedSeedContext(
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
                    CertifiedSeedCausalOwnerKind)
                : causalRunId + ":certified-seed-ledger";
            fixture = new Fixture(ledgerRunId);
            runtime = fixture.CreateCertifiedSeedRuntime();
            facilityId = fixture.CertifiedFacility
                .RequirePersistentInstanceId().Value;
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
            ? CertifiedSeedCausalOwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            CertifiedSeedWorldSaveData state = runtime.Capture();
            return new Dictionary<string, object>
            {
                ["certifiedSeed.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "certified-seed-owner:ephemeral",
                ["certifiedSeed.facilityId"] = facilityId,
                ["certifiedSeed.cropId"] = fixture.Crop.CropId,
                ["certifiedSeed.stage"] = Stage,
                ["certifiedSeed.nextOrderSequence"] = state.nextOrderSequence,
                ["certifiedSeed.lastProcessedOperatingDay"] =
                    state.lastProcessedOperatingDay,
                ["certifiedSeed.operatingDay"] = fixture.OperatingDay,
                ["certifiedSeed.activeOrderCount"] = state.orders.Count,
                ["certifiedSeed.certifiedLotCount"] =
                    fixture.CountCertifiedSeedLots(),
                ["certifiedSeed.exactOutcomeCount"] = ExactOutcomeCount(),
                ["certifiedSeed.firstExecuted"] = firstExecuted,
                ["certifiedSeed.repeatExecuted"] = repeatExecuted
            };
        }

        internal bool CompleteFirstOrder()
        {
            ThrowIfDisposed();
            if (Stage != 0
                || !CompleteOrder(CertifiedSeedFirstCompletionActionId, 23))
            {
                return false;
            }
            firstExecuted = true;
            ValidateState();
            return true;
        }

        internal bool CompleteRepeatOrder()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            fixture.Clock.AdvanceGameHours(GameCalendarRules.HoursPerDay);
            if (!CompleteOrder(CertifiedSeedRepeatCompletionActionId, 24))
                return false;
            repeatExecuted = true;
            ValidateState();
            return true;
        }

        internal GameplayOutcomeReplayIdentity RequireFirstOutcome() =>
            RequireOutcome(1);

        internal GameplayOutcomeReplayIdentity RequireRepeatOutcome() =>
            RequireOutcome(2);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(fixture.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral certified-seed fixtures have no checkpoint.");
            }
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                CertifiedSeedCausalOwnerKind,
                CertifiedSeedCausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = CertifiedSeedCausalStateSchema,
                    ["facilityId"] = facilityId,
                    ["cropId"] = fixture.Crop.CropId,
                    ["stage"] = Stage,
                    ["certifiedSeedState"] = JObject.FromObject(
                        runtime.Capture()),
                    ["physicalItems"] = JObject.FromObject(
                        fixture.WorldItems.Capture()),
                    ["clockDay"] = fixture.Clock.Day,
                    ["clockHour"] = fixture.Clock.Hour,
                    ["clockTime"] = fixture.Clock.RawTime,
                    ["clockDeltaTime"] = fixture.Clock.RawDeltaTime,
                    ["clockRunning"] = fixture.Clock.IsRunning,
                    ["firstExecuted"] = firstExecuted,
                    ["repeatExecuted"] = repeatExecuted
                },
                fixture.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            fixture?.Dispose();
        }

        private int Stage => repeatExecuted ? 2 : firstExecuted ? 1 : 0;

        private void PrepareInitialState()
        {
            fixture.AddSeed(
                WorldItemStackState.Loose,
                string.Empty,
                fixture.CertifiedFacility.centerPos,
                pathogenLoad: 20f);
            WorldItemRepositoryEditorAccess.AddStack(
                fixture.Repository,
                CertificationKitItemId,
                1,
                WorldItemStackState.Loose,
                position: fixture.CertifiedFacility.centerPos);
        }

        private bool CompleteOrder(string actionId, int expectedOperatingDay)
        {
            if (fixture.OperatingDay != expectedOperatingDay
                || !runtime.TryPlan(
                    actionId,
                    fixture.Crop.CropId,
                    facilityId,
                    out DomainFailure failure)
                || failure.IsFailure)
            {
                return false;
            }

            CertifiedSeedOrderSaveData order = runtime.Capture().orders
                .Single(value => string.Equals(
                    value.actionId,
                    actionId,
                    StringComparison.Ordinal));
            string deliveredSeed = fixture.AddSeed(
                WorldItemStackState.FacilityBuffer,
                order.destinationId,
                fixture.CertifiedFacility.centerPos,
                pathogenLoad: 20f);
            string deliveredKit = WorldItemRepositoryEditorAccess.AddStack(
                fixture.Repository,
                CertificationKitItemId,
                1,
                WorldItemStackState.FacilityBuffer,
                destinationId: order.destinationId,
                position: fixture.CertifiedFacility.centerPos);
            int certifiedBefore = fixture.CountCertifiedSeedLots();

            if (!runtime.IsPlanReadyForCompletion(actionId)
                || runtime.CompleteDeliveredPlans(expectedOperatingDay) != 1
                || runtime.Capture().orders.Count != 0
                || fixture.Repository.GetEditorTestQuantity(deliveredSeed) != 0
                || fixture.Repository.GetEditorTestQuantity(deliveredKit) != 0
                || fixture.CountCertifiedSeedLots() != certifiedBefore + 1)
            {
                return false;
            }
            return RequireOutcome(Stage + 1).HasCanonicalPayloadHash;
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(int expectedCount)
        {
            GameplayOutcomeSnapshot[] matches = fixture.Ledger
                .CaptureGameplayOutcomes()
                .exactOutcomes
                .Where(value => value != null
                    && string.Equals(
                        value.producerId,
                        EnvironmentOutcomeIds.CertifiedSeedProducer,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.outcomeTypeId,
                        EnvironmentOutcomeIds.CertifiedSeedCompleted.Value,
                        StringComparison.Ordinal))
                .OrderBy(value => value.commitRevision)
                .ThenBy(value => value.localResultIndex)
                .ToArray();
            RequireCertifiedSeedCausal(matches.Length == expectedCount,
                "Certified-seed exact outcome count changed. expected="
                + expectedCount + "; actual=" + matches.Length);
            GameplayOutcomeSnapshot snapshot = matches[^1];
            RequireCertifiedSeedCausal(
                snapshot.lifecycle
                    == GameplayOutcomeRecordLifecycle.PublishedAcknowledged
                && snapshot.status == GameplayOutcomeStatus.Succeeded,
                "Certified-seed outcome is not PublishedAcknowledged/Succeeded.");
            GameplayResultKey key = new(
                snapshot.producerId,
                new GameplayOperationId(snapshot.operationId),
                snapshot.commitRevision,
                snapshot.localResultIndex);
            RequireCertifiedSeedCausal(
                fixture.Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && fixture.Ledger.TryGetExact(identity.OutcomeId, out _),
                "Certified-seed replay identity is not canonical.");
            return identity;
        }

        private int ExactOutcomeCount() => fixture.Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes
            .Count(value => value != null
                && string.Equals(
                    value.producerId,
                    EnvironmentOutcomeIds.CertifiedSeedProducer,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.outcomeTypeId,
                    EnvironmentOutcomeIds.CertifiedSeedCompleted.Value,
                    StringComparison.Ordinal));

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                CertifiedSeedCausalOwnerKind,
                CertifiedSeedCausalOwnerRestoreKind,
                fixture.Ledger);
            RequireCertifiedSeedFields(
                state,
                "certified-seed owner state",
                "schemaVersion",
                "schema",
                "facilityId",
                "cropId",
                "stage",
                "certifiedSeedState",
                "physicalItems",
                "clockDay",
                "clockHour",
                "clockTime",
                "clockDeltaTime",
                "clockRunning",
                "firstExecuted",
                "repeatExecuted");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals(
                    (string)state["schema"],
                    CertifiedSeedCausalStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["facilityId"],
                    facilityId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["cropId"],
                    fixture.Crop.CropId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Certified-seed checkpoint identities changed.");
            }

            fixture.WorldItems.Restore(
                state["physicalItems"].ToObject<DungeonPhysicalItemSaveData>()
                ?? throw new InvalidOperationException(
                    "Certified-seed physical-item checkpoint is missing."));
            CertifiedSeedWorldSaveData runtimeState =
                state["certifiedSeedState"]
                    .ToObject<CertifiedSeedWorldSaveData>()
                ?? throw new InvalidOperationException(
                    "Certified-seed runtime checkpoint is missing.");
            runtime.Restore(runtime.BuildRestore(runtimeState));
            fixture.Clock.Restore(
                (int)state["clockDay"],
                (int)state["clockHour"],
                (float)state["clockTime"],
                (float)state["clockDeltaTime"],
                (bool)state["clockRunning"]);
            firstExecuted = (bool)state["firstExecuted"];
            repeatExecuted = (bool)state["repeatExecuted"];
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Certified-seed checkpoint stage disagrees with action history.");
            }
        }

        private void ValidateState()
        {
            CertifiedSeedWorldSaveData state = runtime.Capture();
            int expectedDay = Stage switch
            {
                0 => 0,
                1 => 23,
                2 => 24,
                _ => throw new InvalidOperationException(
                    "Certified-seed stage is outside the supported range.")
            };
            RequireCertifiedSeedCausal(
                state.orders.Count == 0
                && state.nextOrderSequence == Stage
                && state.lastProcessedOperatingDay == expectedDay
                && fixture.CountCertifiedSeedLots() == Stage
                && ExactOutcomeCount() == Stage
                && fixture.OperatingDay == (Stage == 2 ? 24 : 23)
                && firstExecuted == (Stage >= 1)
                && repeatExecuted == (Stage >= 2),
                "Certified-seed runtime, physical inventory, clock, and ledger diverged.");
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(CausalCertifiedSeedContext));
        }
    }

    private sealed class CertifiedSeedCausalFixtureSet : IDisposable
    {
        private readonly IProductionCausalActionFixture[] fixtures;

        internal CertifiedSeedCausalFixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> fixtures)
        {
            this.fixtures = fixtures?.ToArray()
                ?? throw new ArgumentNullException(nameof(fixtures));
            RequireCertifiedSeedCausal(this.fixtures.Length == 2,
                "Certified-seed causal fixture set is incomplete.");
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

    private static void RequireCertifiedSeedFields(
        JObject value,
        string label,
        params string[] fields)
    {
        if (value == null)
            throw new InvalidOperationException(label + " is missing.");
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

    private static void RequireCertifiedSeedCausal(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
