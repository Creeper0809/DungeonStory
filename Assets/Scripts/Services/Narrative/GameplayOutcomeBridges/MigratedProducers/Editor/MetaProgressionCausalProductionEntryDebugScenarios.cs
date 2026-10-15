#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MetaProgressionCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:meta-run-progression";
    internal const string RunResultActionId = "action:run-result-ready";
    internal const string UpgradeActionId = "action:meta-upgrade-purchased";

    private const string OwnerKind = "owner-kind:meta-run-progression";
    private const string DomainStateSchema =
        "dungeonstory-causal-meta-run-progression-owner-state@1";
    private const string UpgradeId = "meta:causal-legacy-ledger";
    private const string UpgradeName = "계승의 장부";
    private const string OwnerName = "연화";
    private const string EndReason = "던전 계승 준비";
    private const int UpgradeCost = 10;
    private const int ExpectedLegacyCurrency = 35;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
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
                "A meta-progression checkpoint requires an owner ID.",
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
                RunResultActionId,
                "run",
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.RunResultReadyEvent)
                    .OutcomeTypeId,
                context.CaptureState,
                context.CompleteRun,
                context.RequireRunResultOutcome,
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
                UpgradeActionId,
                "meta",
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent)
                    .OutcomeTypeId,
                context.CaptureState,
                context.PurchaseUpgrade,
                context.RequireUpgradeOutcome,
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
        const string OwnerIdValue = "meta-owner:causal-run-progression:focused";
        JObject resultCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-meta-progression:result",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture result = fixtures.Get(RunResultActionId);
            IReadOnlyDictionary<string, object> before = result.CaptureState();
            Require((int)before["metaProgression.stage"] == 0
                    && (int)before["metaProgression.availableCurrency"] == 0
                    && (int)before["metaProgression.exactOutcomeCount"] == 0,
                "Meta-progression owner did not start empty.");
            Require(result.Execute(), "The real run result did not complete.");
            Require(result.RequireExactOutcome().HasCanonicalPayloadHash,
                "The run-result outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = result.CaptureState();
            Require((int)after["metaProgression.stage"] == 1
                    && (int)after["metaProgression.completedRunCount"] == 1
                    && (bool)after["metaProgression.ended"]
                    && (int)after["metaProgression.lifetimeEarnedCurrency"]
                        == ExpectedLegacyCurrency
                    && (int)after["metaProgression.availableCurrency"]
                        == ExpectedLegacyCurrency
                    && (int)after["metaProgression.exactOutcomeCount"] == 1,
                "Run completion did not retain its real meta state.");
            resultCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)result)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-meta-progression:upgrade",
                   OwnerIdValue,
                   resultCheckpoint)))
        {
            IProductionCausalActionFixture result = fixtures.Get(RunResultActionId);
            IProductionCausalActionFixture upgrade = fixtures.Get(UpgradeActionId);
            IReadOnlyDictionary<string, object> restored = upgrade.CaptureState();
            Require((int)restored["metaProgression.stage"] == 1
                    && !result.Execute(),
                "Fresh composition restore lost or duplicated the run result.");
            Require(upgrade.Execute(), "The restored meta upgrade did not purchase.");
            Require(upgrade.RequireExactOutcome().HasCanonicalPayloadHash,
                "The upgrade outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = upgrade.CaptureState();
            Require((int)after["metaProgression.stage"] == 2
                    && (int)after["metaProgression.spentCurrency"] == UpgradeCost
                    && (int)after["metaProgression.availableCurrency"]
                        == ExpectedLegacyCurrency - UpgradeCost
                    && (int)after["metaProgression.upgradeLevel"] == 1
                    && (int)after["metaProgression.exactOutcomeCount"] == 2,
                "Upgrade purchase did not preserve the run result and advance meta state.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)upgrade)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-meta-progression:terminal",
                   OwnerIdValue,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture result = fixtures.Get(RunResultActionId);
            IProductionCausalActionFixture upgrade = fixtures.Get(UpgradeActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)result)
                .CaptureOwnerCheckpoint();
            Require(!result.Execute() && !upgrade.Execute(),
                "Terminal meta-progression owner accepted a replay.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)upgrade)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected meta-progression replay mutated the owner checkpoint.");
        }

        JObject currencyTamper = (JObject)resultCheckpoint.DeepClone();
        ((JObject)currencyTamper["domainState"])["metaProgression"][
            "lifetimeEarnedCurrency"] = ExpectedLegacyCurrency + 1;
        Require(RejectCheckpoint(OwnerIdValue, currencyTamper, "currency-tamper"),
            "A meta checkpoint with changed run currency was accepted.");

        JObject identityTamper = (JObject)resultCheckpoint.DeepClone();
        identityTamper["ownerId"] = "meta-owner:causal-run-progression:tampered";
        Require(RejectCheckpoint(OwnerIdValue, identityTamper, "identity-tamper"),
            "A meta checkpoint with changed owner identity was accepted.");

        if (log)
        {
            Debug.Log(
                "Run result -> restored meta upgrade purchase causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Meta-progression causal owner family returned false.");
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
                "run:causal-meta-progression:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static DomainFactDispatcher CreateMetaFacts(
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(
            outcomes.Recorder,
            outcomes.Ledger);
        try
        {
            dispatcher.Subscribe(
                "fixture:meta-upgrade-purchased",
                new MigratedProducerDomainFactProbeObserver<
                    MetaUpgradePurchaseDomainFact>(
                    MetaProgressionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            dispatcher.Subscribe(
                "fixture:meta-run-result",
                new MigratedProducerDomainFactProbeObserver<
                    MetaRunResultDomainFact>(
                    MetaProgressionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly string ledgerRunId;
        private readonly GameObject root;
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly DomainFactDispatcher domainFacts;
        private readonly MetaProgressionRuntime runtime;
        private readonly MetaProgressionSaveSection saveSection;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":meta-progression-ledger";
            var aggregateRootStore = new DungeonRuntimeAggregateRootStore();
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ledgerRunId,
                aggregateRootStore);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            domainFacts = CreateMetaFacts(outcomes, probe);
            root = new GameObject("Causal meta run progression owner");
            runtime = root.AddComponent<MetaProgressionRuntime>();
            runtime.Construct(
                new MetaRunResultBuilder(),
                FixedMetaRuntimeApplicationPort.Instance,
                new UnityGameClock(),
                new SingleMetaUpgradeCatalog(new MetaUpgradeDefinition(
                    UpgradeId,
                    MetaProgressionBranch.OperationKnowledge,
                    UpgradeName,
                    "한 번의 런 결과를 계승 기록으로 보존한다.",
                    UpgradeCost,
                    1,
                    Array.Empty<IMetaUpgradeEffect>())),
                aggregateRootStore);
            runtime.ConstructDomainFacts(domainFacts);
            runtime.SetShowRunResultPanel(false);
            saveSection = new MetaProgressionSaveSection(runtime);
            try
            {
                if (checkpoint != null)
                    RestoreOwnerCheckpoint(checkpoint);
                probe.ClearObservations();
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
            RunResultSnapshot latest = runtime.LatestResult;
            return new Dictionary<string, object>
            {
                ["metaProgression.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "meta-owner:ephemeral-run-progression",
                ["metaProgression.stage"] = Stage,
                ["metaProgression.lifetimeEarnedCurrency"] =
                    runtime.State.LifetimeEarnedCurrency,
                ["metaProgression.spentCurrency"] = runtime.State.SpentCurrency,
                ["metaProgression.availableCurrency"] =
                    runtime.State.AvailableCurrency,
                ["metaProgression.completedRunCount"] =
                    runtime.State.CompletedRunCount,
                ["metaProgression.ended"] = runtime.HasEnded,
                ["metaProgression.hasLatestResult"] = latest != null,
                ["metaProgression.latestOwnerName"] =
                    latest?.ownerName ?? "none",
                ["metaProgression.latestEndReason"] =
                    latest?.endReason ?? "none",
                ["metaProgression.latestOutcome"] =
                    latest?.outcome.ToString() ?? "None",
                ["metaProgression.latestLegacyCurrency"] =
                    latest?.legacyCurrency ?? 0,
                ["metaProgression.upgradeId"] = UpgradeId,
                ["metaProgression.upgradeLevel"] =
                    runtime.State.GetUpgradeLevel(UpgradeId),
                ["metaProgression.runResultOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.RunResultReadyEvent),
                ["metaProgression.upgradeOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent),
                ["metaProgression.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool CompleteRun()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            RunResultSnapshot result = runtime.EndRun(
                OwnerName,
                EndReason,
                DungeonRunOutcome.Victory);
            bool valid = result != null
                && result.legacyCurrency == ExpectedLegacyCurrency
                && Stage == 1;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool PurchaseUpgrade()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            bool purchased = runtime.TryPurchaseUpgrade(UpgradeId, out _);
            bool valid = purchased && Stage == 2;
            if (valid)
                ValidateState();
            return valid;
        }

        internal GameplayOutcomeReplayIdentity RequireRunResultOutcome() =>
            RequireOutcome(
                MigratedProducerOutcomeKind.RunResultReadyEvent,
                "meta-run-result:sequence:1",
                OwnerName);

        internal GameplayOutcomeReplayIdentity RequireUpgradeOutcome() =>
            RequireOutcome(
                MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent,
                "meta-upgrade-purchase:" + UpgradeId,
                UpgradeName);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral meta-progression fixtures have no checkpoint.");
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
                    ["stage"] = Stage,
                    ["upgradeId"] = UpgradeId,
                    ["upgradeCost"] = UpgradeCost,
                    ["metaProgression"] = JObject.Parse(saveSection.Capture()),
                    ["outcomeOwners"] = JObject.Parse(JsonUtility.ToJson(
                        outcomes.Transaction.Capture()))
                },
                outcomes.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            domainFacts?.Dispose();
            if (root != null)
                Object.DestroyImmediate(root);
        }

        private int Stage
        {
            get
            {
                int runResults = OutcomeCount(
                    MigratedProducerOutcomeKind.RunResultReadyEvent);
                int upgrades = OutcomeCount(
                    MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent);
                if (runResults == 0
                    && upgrades == 0
                    && !runtime.HasEnded
                    && runtime.State.CompletedRunCount == 0
                    && runtime.State.GetUpgradeLevel(UpgradeId) == 0)
                {
                    return 0;
                }
                if (runResults == 1
                    && upgrades == 0
                    && runtime.HasEnded
                    && runtime.State.CompletedRunCount == 1
                    && runtime.State.GetUpgradeLevel(UpgradeId) == 0)
                {
                    return 1;
                }
                if (runResults == 1
                    && upgrades == 1
                    && runtime.HasEnded
                    && runtime.State.CompletedRunCount == 1
                    && runtime.State.GetUpgradeLevel(UpgradeId) == 1)
                {
                    return 2;
                }
                return -1;
            }
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(
            MigratedProducerOutcomeKind kind,
            string entityId,
            string displayName)
        {
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    kind,
                    probe);
            if (!identity.HasCanonicalPayloadHash
                || !outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                || exact.absoluteDay != 1
                || exact.status != GameplayOutcomeStatus.Succeeded
                || exact.participants == null
                || exact.participants.Count != 1
                || exact.subjects == null
                || exact.subjects.Count != 1
                || !string.Equals(
                    exact.participants[0].entityId,
                    entityId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exact.participants[0].displayText,
                    displayName,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Meta-progression production entry did not publish its exact outcome: "
                    + kind);
            }
            return identity;
        }

        private int OutcomeCount(MigratedProducerOutcomeKind kind)
        {
            string type = MigratedProducerOutcomeCatalog.Get(kind)
                .OutcomeTypeId.Value;
            return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(
                        value.outcomeTypeId,
                        type,
                        StringComparison.Ordinal));
        }

        private int ExactOutcomeCount() => outcomes.Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes.Count;

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
                "meta-progression owner state",
                "schemaVersion",
                "schema",
                "stage",
                "upgradeId",
                "upgradeCost",
                "metaProgression",
                "outcomeOwners");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals((string)state["schema"], DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["upgradeId"], UpgradeId,
                    StringComparison.Ordinal)
                || (int)state["upgradeCost"] != UpgradeCost
                || state["metaProgression"] is not JObject metaJson
                || state["outcomeOwners"] is not JObject ownerJson)
            {
                throw new InvalidOperationException(
                    "Meta-progression checkpoint identity changed.");
            }

            DungeonMigratedProducerOutcomeSaveData ownerData =
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    ownerJson.ToString(Formatting.None));
            outcomes.Transaction.Restore(
                outcomes.Transaction.PrepareRestore(ownerData));
            IDungeonSaveRestoreStage staged = saveSection.StageRestore(
                metaJson.ToString(Formatting.None),
                saveSection.SectionVersion,
                new DungeonGameRestoreReport());
            staged.Commit(new DungeonGameRestoreReport());
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Meta-progression checkpoint stage disagrees with its saved state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            RunResultSnapshot latest = runtime.LatestResult;
            int expectedEarned = stage == 0 ? 0 : ExpectedLegacyCurrency;
            int expectedSpent = stage == 2 ? UpgradeCost : 0;
            bool valid = stage is >= 0 and <= 2
                && runtime.State.LifetimeEarnedCurrency == expectedEarned
                && runtime.State.SpentCurrency == expectedSpent
                && runtime.State.AvailableCurrency == expectedEarned - expectedSpent
                && runtime.State.CompletedRunCount == (stage == 0 ? 0 : 1)
                && runtime.HasEnded == (stage != 0)
                && runtime.State.GetUpgradeLevel(UpgradeId) == (stage == 2 ? 1 : 0)
                && ExactOutcomeCount() == stage;
            if (stage == 0)
            {
                valid &= latest == null;
            }
            else
            {
                valid &= latest != null
                    && string.Equals(latest.ownerName, OwnerName,
                        StringComparison.Ordinal)
                    && string.Equals(latest.endReason, EndReason,
                        StringComparison.Ordinal)
                    && latest.outcome == DungeonRunOutcome.Victory
                    && latest.survivedOperatingDays == 1
                    && latest.legacyCurrency == ExpectedLegacyCurrency;
            }
            if (!valid)
            {
                throw new InvalidOperationException(
                    "Meta progression, outcome owner and exact ledger diverged.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal FixtureSet(IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures ?? throw new ArgumentNullException(nameof(fixtures));

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

    private sealed class SingleMetaUpgradeCatalog : IMetaUpgradeDefinitionCatalog
    {
        private readonly MetaUpgradeDefinition definition;

        internal SingleMetaUpgradeCatalog(MetaUpgradeDefinition definition) =>
            this.definition = definition
                ?? throw new ArgumentNullException(nameof(definition));

        public IReadOnlyCollection<MetaUpgradeDefinition> All =>
            new[] { definition };
        public MetaUpgradeDefinition Get(string id) =>
            string.Equals(id, definition.id, StringComparison.Ordinal)
                ? definition
                : null;
        public MetaUpgradeDefinition Require(string id) => Get(id)
            ?? throw new KeyNotFoundException(id);
    }

    private sealed class FixedMetaRuntimeApplicationPort : IMetaRuntimeApplicationPort
    {
        internal static readonly FixedMetaRuntimeApplicationPort Instance = new();

        public void Bind(IMetaRuntimeEventSink runtime) { }
        public void Unbind(IMetaRuntimeEventSink runtime) { }
        public MetaRunEnvironmentSnapshot CaptureRunEnvironment() => new(
            1f,
            DungeonDifficulty.Normal,
            DungeonSurvivalPressure.Standard,
            Array.Empty<string>(),
            Array.Empty<CommittedRunChoiceSnapshot>());
        public void PublishUpgradePurchased(
            MetaUpgradePurchasedEvent purchasedEvent,
            string message) { }
        public void PublishRunResult(RunResultReadyEvent readyEvent) { }
        public void ShowRunResult(RunResultSnapshot result) { }
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
