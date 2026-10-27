#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:observed-meal-incident-resolution";
    internal const string ObserveIncidentActionId =
        "action:observe-contaminated-meal-incident";
    internal const string ResolveIncidentActionId =
        "action:resolve-observed-meal-incident";

    private const string OwnerKind =
        "owner-kind:observed-meal-incident-resolution";
    private const string DomainStateSchema =
        "dungeonstory-causal-observed-meal-incident-resolution-owner-state@1";
    private const string CustomerId =
        "character:causal-observed-meal:customer";
    private const string FoodId = "food:lavish-meat";
    private const string StackId = "stack:causal-observed-meal:contaminated";
    private const string DefinitionId = "service-incident:contamination";
    private const string ResolutionId = "quarantine";
    private const int ObservationDay = 42;
    private const int ResolutionDay = 43;

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
                "An observed meal-incident checkpoint requires an owner ID.",
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
            Fixture(
                ObserveIncidentActionId,
                "survival",
                MigratedProducerOutcomeKind.ObservedMealIncidentCaptureResult,
                context,
                context.ObserveIncident,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind
                        .ObservedMealIncidentCaptureResult,
                    expectedSequence: 1,
                    expectedDay: ObservationDay),
                lifetime),
            Fixture(
                ResolveIncidentActionId,
                "run",
                MigratedProducerOutcomeKind.V20ResolvedEventResult,
                context,
                context.ResolveIncident,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.V20ResolvedEventResult,
                    expectedSequence: 2,
                    expectedDay: ResolutionDay),
                lifetime)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string OwnerIdValue =
            "owner:causal-observed-meal-incident-resolution:focused";
        JObject observedCheckpoint;
        string occurrenceId;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-observed-meal:observe",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture observe = fixtures.Get(
                ObserveIncidentActionId);
            Require((int)observe.CaptureState()["mealIncident.stage"] == 0,
                "Observed meal-incident owner did not start empty.");
            Require(observe.Execute(),
                "The real observed meal-incident entry was rejected.");
            Require(observe.RequireExactOutcome().HasCanonicalPayloadHash,
                "The observed meal-incident result has no canonical hash.");
            IReadOnlyDictionary<string, object> after = observe.CaptureState();
            Require(
                (int)after["mealIncident.stage"] == 1
                && (int)after["mealIncident.successfulReceiptCount"] == 1
                && (int)after["mealIncident.activeEventCount"] == 1
                && (int)after["mealIncident.resolvedEventCount"] == 0,
                "Observed contamination did not persist its active incident.");
            occurrenceId = (string)after["mealIncident.occurrenceId"];
            Require(!string.IsNullOrWhiteSpace(occurrenceId),
                "Observed contamination did not expose its occurrence ID.");
            observedCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)observe)
                .CaptureOwnerCheckpoint();
        }

        JObject resolvedCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-observed-meal:resolve",
                   OwnerIdValue,
                   observedCheckpoint)))
        {
            IProductionCausalActionFixture observe = fixtures.Get(
                ObserveIncidentActionId);
            IProductionCausalActionFixture resolve = fixtures.Get(
                ResolveIncidentActionId);
            Require(!observe.Execute(),
                "Fresh composition replayed the observed contamination.");
            Require(resolve.Execute(),
                "The real campaign-event resolution entry was rejected.");
            Require(resolve.RequireExactOutcome().HasCanonicalPayloadHash,
                "The campaign-event resolution has no canonical hash.");
            IReadOnlyDictionary<string, object> after = resolve.CaptureState();
            Require(
                (int)after["mealIncident.stage"] == 2
                && (int)after["mealIncident.successfulReceiptCount"] == 1
                && (int)after["mealIncident.activeEventCount"] == 0
                && (int)after["mealIncident.resolvedEventCount"] == 1
                && (string)after["mealIncident.occurrenceId"] == occurrenceId
                && (string)after["mealIncident.resolutionId"] == ResolutionId
                && (int)after["mealIncident.workDelayDays"] == 2,
                "Campaign resolution did not consume the restored incident exactly.");
            resolvedCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)resolve)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-observed-meal:terminal",
                   OwnerIdValue,
                   resolvedCheckpoint)))
        {
            IProductionCausalActionFixture observe = fixtures.Get(
                ObserveIncidentActionId);
            IProductionCausalActionFixture resolve = fixtures.Get(
                ResolveIncidentActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)observe)
                .CaptureOwnerCheckpoint();
            Require(!observe.Execute() && !resolve.Execute(),
                "Terminal observed meal-incident owner accepted a replay.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)observe)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected observed meal-incident replay mutated its checkpoint.");
        }

        JObject campaignTamper = (JObject)observedCheckpoint.DeepClone();
        ((JArray)((JObject)campaignTamper["domainState"])["society"]
            ["successfulIncidentOperations"]).Clear();
        Require(RejectCheckpoint(
                OwnerIdValue,
                campaignTamper,
                "campaign-tamper"),
            "An observed meal incident without its source receipt was accepted.");

        JObject ledgerTamper = (JObject)resolvedCheckpoint.DeepClone();
        ((JArray)((JObject)ledgerTamper["outcomeLedger"])["exactOutcomes"])
            .RemoveAt(0);
        Require(RejectCheckpoint(
                OwnerIdValue,
                ledgerTamper,
                "ledger-tamper"),
            "A resolved meal incident with a missing exact outcome was accepted.");

        if (log)
        {
            Debug.Log(
                "Observed contaminated meal -> restored exact campaign-event resolution causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Observed meal-incident resolution owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static IProductionCausalActionFixture Fixture(
        string actionId,
        string domainId,
        MigratedProducerOutcomeKind kind,
        Context context,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireOutcome,
        SharedProductionCausalFixtureLifetime lifetime) =>
        new DelegateProductionCausalActionFixture(
            actionId,
            domainId,
            MigratedProducerOutcomeCatalog.Get(kind).OutcomeTypeId,
            context.CaptureState,
            execute,
            requireOutcome,
            context.CaptureOutcomeEvidence,
            lifetime.Release,
            context.PersistentOwnerId,
            context.PersistentOwnerKind,
            context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
            context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
            context.HasPersistentOwner ? context : null);

    private static bool RejectCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-observed-meal:" + suffix,
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
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly SocialRitualFactEditorFixture facts;
        private readonly V20StoryContentCatalog catalog;
        private readonly V20CampaignRuntime campaign;
        private readonly ObservedMealIncidentSnapshot evidence;
        private readonly V20DailyEventContext observationContext;
        private readonly V20ContentResolutionService resolution;
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
                : causalRunId + ":observed-meal-incident-resolution-ledger";
            var aggregateRootStore = new DungeonRuntimeAggregateRootStore();
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ledgerRunId,
                aggregateRootStore);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            facts = new SocialRitualFactEditorFixture(outcomes, probe);
            catalog = new V20StoryContentCatalog(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()));
            campaign = new V20CampaignRuntime(
                aggregateRootStore,
                catalog,
                null,
                EmptySeasonalFeedTargetQuery.Instance,
                facts.Journal);
            evidence = new ObservedMealIncidentSnapshot(
                new ConsumableOperationId(
                    "consumable-operation:causal-observed-meal:contamination"),
                new CharacterId(CustomerId),
                new ItemDefinitionId(FoodId),
                new ItemStackId(StackId),
                default,
                fieldMeal: true,
                new CoreGridCell(3, 4),
                ObservationDay,
                observedPolicyViolation: false,
                observedDowned: false,
                observedDead: false,
                observedContaminated: true);
            observationContext = new V20DailyEventContext
            {
                AbsoluteDay = ObservationDay,
                RunSeed = 157181,
                Season = Season.Spring
            };
            resolution = DungeonCharacterRegistration.CreateContentResolutionForDebug(
                campaign,
                catalog,
                AlwaysSatisfiedContentRequirements.Instance,
                EmptyCharacterWorldQuery.Instance,
                EmptyStockQuery.Instance,
                EmptyAtomicItemConsumption.Instance,
                facts.Journal);

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
            SocietyEventWorldSaveData society = campaign.CaptureSociety();
            V20ActiveEventSaveData occurrence = FindOccurrence(society);
            return new Dictionary<string, object>
            {
                ["mealIncident.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-observed-meal-incident-resolution",
                ["mealIncident.stage"] = Stage,
                ["mealIncident.absoluteDay"] = CurrentAbsoluteDay,
                ["mealIncident.customerId"] = CustomerId,
                ["mealIncident.itemDefinitionId"] = FoodId,
                ["mealIncident.itemStackId"] = StackId,
                ["mealIncident.definitionId"] = DefinitionId,
                ["mealIncident.occurrenceId"] = occurrence?.instanceId
                    ?? string.Empty,
                ["mealIncident.resolutionId"] = occurrence?.resolutionId
                    ?? string.Empty,
                ["mealIncident.observedCause"] =
                    occurrence?.observedMealIncident?.observedCause
                    ?? string.Empty,
                ["mealIncident.successfulReceiptCount"] =
                    society.successfulIncidentOperations.Count,
                ["mealIncident.activeEventCount"] = society.activeEvents.Count,
                ["mealIncident.resolvedEventCount"] =
                    society.recentResolvedEvents.Count,
                ["mealIncident.workDelayDays"] =
                    campaign.GetRemainingDays(DefinitionId),
                ["mealIncident.observationOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .ObservedMealIncidentCaptureResult),
                ["mealIncident.resolutionOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.V20ResolvedEventResult),
                ["mealIncident.migratedOwnerCount"] =
                    outcomes.Transaction.Capture().owners.Count,
                ["mealIncident.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool ObserveIncident()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            ObservedMealIncidentCaptureResult result =
                campaign.CaptureObservedMealIncident(
                    evidence,
                    observationContext);
            Require(
                result.Created
                && result.IncidentKind == ServiceIncidentKind.Contamination
                && !result.Failure.IsFailure,
                "Observed meal-incident production entry failed: "
                + result.Failure);
            ValidateState();
            return Stage == 1;
        }

        internal bool ResolveIncident()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            V20ActiveEventSaveData active =
                campaign.CaptureSociety().activeEvents.Single();
            bool accepted = resolution.TryExecute(
                new ContentResolutionRequest
                {
                    ActionId =
                        "content-resolution:causal-observed-meal:quarantine",
                    Kind = ContentResolutionRequestKind.SocietyChoice,
                    InstanceId = active.instanceId,
                    ChoiceId = ResolutionId,
                    AbsoluteDay = ResolutionDay,
                    Requirements = new RunMilestoneEvaluationSnapshot()
                },
                out ContentResolutionResult result,
                out DomainFailure failure);
            Require(
                accepted
                && !failure.IsFailure
                && result != null
                && result.Resolutions.Count == 1
                && string.Equals(
                    result.Resolutions[0].InstanceId,
                    active.instanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    result.Resolutions[0].DefinitionId,
                    DefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(
                    result.Resolutions[0].ResolutionId,
                    ResolutionId,
                    StringComparison.Ordinal),
                "Campaign-event production resolution failed: " + failure);
            ValidateState();
            return Stage == 2;
        }

        internal GameplayOutcomeReplayIdentity RequireOutcome(
            MigratedProducerOutcomeKind kind,
            int expectedSequence,
            int expectedDay)
        {
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    kind,
                    probe);
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(kind);
            bool detailedResolution = kind
                == MigratedProducerOutcomeKind.V20ResolvedEventResult;
            if (!identity.HasCanonicalPayloadHash
                || !outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                || exact.sequence != expectedSequence
                || exact.absoluteDay != expectedDay
                || exact.status != GameplayOutcomeStatus.Succeeded
                || exact.ownerRevision != 1L
                || exact.commitRevision != 1L
                || !string.Equals(
                    exact.producerId,
                    definition.ProducerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exact.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal)
                || exact.subjects == null
                || (!detailedResolution && exact.subjects.Count != 1)
                || (detailedResolution
                    && (!exact.participants.Any(value => value != null
                        && string.Equals(value.entityKindId, "character",
                            StringComparison.Ordinal)
                        && string.Equals(value.entityId, CustomerId,
                            StringComparison.Ordinal)
                        && string.Equals(value.roleId, "actor",
                            StringComparison.Ordinal))
                    || !exact.participants.Any(value => value != null
                        && string.Equals(value.entityKindId, "operation",
                            StringComparison.Ordinal)
                        && string.Equals(value.entityId, DefinitionId,
                            StringComparison.Ordinal)
                        && string.Equals(value.roleId, "target",
                            StringComparison.Ordinal))
                    || exact.metrics.Count != 1
                    || !exact.metrics.Any(value => value != null
                        && string.Equals(value.metricId, "amount",
                            StringComparison.Ordinal)
                        && Math.Abs(value.value - 2d) < 0.001d)
                    || !exact.facts.Any(value => value != null
                        && string.Equals(value.factId, "effect-detail",
                            StringComparison.Ordinal)
                        && value.value.Contains("WorkDelayDays",
                            StringComparison.Ordinal)
                        && value.value.Contains("amount=2",
                            StringComparison.Ordinal)
                        && value.value.Contains("childOutcomeId=none",
                            StringComparison.Ordinal))
                    || !exact.facts.Any(value => value != null
                        && string.Equals(value.factId,
                            "state-before-fingerprint",
                            StringComparison.Ordinal)
                        && value.value.StartsWith("sha256:",
                            StringComparison.Ordinal))
                    || !exact.facts.Any(value => value != null
                        && string.Equals(value.factId,
                            "state-after-fingerprint",
                            StringComparison.Ordinal)
                        && value.value.StartsWith("sha256:",
                            StringComparison.Ordinal))
                    || !string.Equals(exact.rootOperationId,
                        "content-resolution:"
                            + NarrativeInferenceHash.ComputeSha256Utf8(
                                "content-resolution:causal-observed-meal:quarantine"),
                        StringComparison.Ordinal)
                    || !string.Equals(exact.causationRelationId,
                        "campaign-event-effect-resolved",
                        StringComparison.Ordinal)))
                || exact.facts == null
                || !exact.facts.Any(value => string.Equals(
                    value.factId,
                    MigratedProducerOutcomeIds.SummaryFact.Value,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Observed meal-incident chain did not publish its exact outcome: "
                    + kind);
            }
            return identity;
        }

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(
                outcomes.Ledger,
                identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral observed meal-incident fixtures have no checkpoint.");
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
                    ["customerId"] = CustomerId,
                    ["itemDefinitionId"] = FoodId,
                    ["itemStackId"] = StackId,
                    ["definitionId"] = DefinitionId,
                    ["resolutionId"] = ResolutionId,
                    ["society"] = JObject.Parse(
                        JsonUtility.ToJson(campaign.CaptureSociety())),
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
            facts?.Dispose();
        }

        private int Stage
        {
            get
            {
                SocietyEventWorldSaveData society = campaign.CaptureSociety();
                int observations = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .ObservedMealIncidentCaptureResult);
                int resolutions = OutcomeCount(
                    MigratedProducerOutcomeKind.V20ResolvedEventResult);
                int exact = ExactOutcomeCount();
                int owners = outcomes.Transaction.Capture().owners.Count;
                if (observations == 0 && resolutions == 0
                    && exact == 0 && owners == 0
                    && society.successfulIncidentOperations.Count == 0
                    && society.activeEvents.Count == 0
                    && society.recentResolvedEvents.Count == 0)
                    return 0;
                if (observations == 1 && resolutions == 0
                    && exact == 1 && owners == 1
                    && society.successfulIncidentOperations.Count == 1
                    && society.activeEvents.Count == 1
                    && society.recentResolvedEvents.Count == 0)
                    return 1;
                if (observations == 1 && resolutions == 1
                    && exact == 2 && owners == 2
                    && society.successfulIncidentOperations.Count == 1
                    && society.activeEvents.Count == 0
                    && society.recentResolvedEvents.Count == 1)
                    return 2;
                return -1;
            }
        }

        private int CurrentAbsoluteDay => Stage switch
        {
            0 => ObservationDay - 1,
            1 => ObservationDay,
            2 => ResolutionDay,
            _ => -1
        };

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
                "observed meal-incident resolution owner state",
                "schemaVersion",
                "schema",
                "stage",
                "customerId",
                "itemDefinitionId",
                "itemStackId",
                "definitionId",
                "resolutionId",
                "society",
                "outcomeOwners");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals(
                    (string)state["schema"],
                    DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["customerId"], CustomerId,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["itemDefinitionId"], FoodId,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["itemStackId"], StackId,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["definitionId"], DefinitionId,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["resolutionId"], ResolutionId,
                    StringComparison.Ordinal)
                || state["society"] is not JObject savedSociety
                || state["outcomeOwners"] is not JObject savedOwners)
            {
                throw new InvalidOperationException(
                    "Observed meal-incident checkpoint identity changed.");
            }

            campaign.PublishSociety(campaign.PrepareSociety(
                JsonUtility.FromJson<SocietyEventWorldSaveData>(
                    savedSociety.ToString(Formatting.None))));
            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    savedOwners.ToString(Formatting.None))));
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Observed meal-incident checkpoint stage disagrees with saved state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            SocietyEventWorldSaveData society = campaign.CaptureSociety();
            V20ActiveEventSaveData occurrence = FindOccurrence(society);
            bool occurrenceShape = stage == 0
                ? occurrence == null
                : occurrence != null
                    && occurrence.hasObservedMealIncident
                    && string.Equals(
                        occurrence.definitionId,
                        DefinitionId,
                        StringComparison.Ordinal)
                    && occurrence.participantCharacterIds.SequenceEqual(
                        new[] { CustomerId })
                    && occurrence.observedMealIncident != null
                    && occurrence.observedMealIncident.incidentKind
                        == ServiceIncidentKind.Contamination
                    && string.Equals(
                        occurrence.observedMealIncident.targetCharacterId,
                        CustomerId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        occurrence.observedMealIncident.itemDefinitionId,
                        FoodId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        occurrence.observedMealIncident.itemStackId,
                        StackId,
                        StringComparison.Ordinal)
                    && occurrence.observedMealIncident.observedCause.Contains(
                        "실제로 섭취한 식사의 오염",
                        StringComparison.Ordinal);
            bool resolvedShape = stage switch
            {
                0 => society.workDelays.Count == 0,
                1 => occurrence != null
                    && !occurrence.resolved
                    && string.IsNullOrEmpty(occurrence.resolutionId)
                    && society.workDelays.Count == 0,
                2 => occurrence != null
                    && occurrence.resolved
                    && string.Equals(
                        occurrence.selectedChoiceId,
                        ResolutionId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        occurrence.resolutionId,
                        ResolutionId,
                        StringComparison.Ordinal)
                    && occurrence.terminalEffectKinds.SequenceEqual(
                        new[] { V20ContentEffectKind.WorkDelayDays })
                    && campaign.GetRemainingDays(DefinitionId) == 2,
                _ => false
            };
            bool receiptShape = stage == 0
                ? society.successfulIncidentOperations.Count == 0
                : society.successfulIncidentOperations.Count == 1
                    && string.Equals(
                        society.successfulIncidentOperations[0].operationId,
                        evidence.OperationId.Value,
                        StringComparison.Ordinal)
                    && string.Equals(
                        society.successfulIncidentOperations[0]
                            .occurrenceInstanceId,
                        occurrence?.instanceId,
                        StringComparison.Ordinal);
            if (!occurrenceShape || !resolvedShape || !receiptShape)
            {
                throw new InvalidOperationException(
                    "Observed meal source receipt, campaign state and exact ledger diverged: stage="
                    + stage
                    + "; receipts="
                    + society.successfulIncidentOperations.Count
                    + "; active=" + society.activeEvents.Count
                    + "; resolved=" + society.recentResolvedEvents.Count);
            }
        }

        private static V20ActiveEventSaveData FindOccurrence(
            SocietyEventWorldSaveData society)
        {
            V20ActiveEventSaveData[] occurrences = society.activeEvents
                .Concat(society.recentResolvedEvents)
                .Where(value => value?.hasObservedMealIncident == true)
                .ToArray();
            if (occurrences.Length > 1)
            {
                throw new InvalidOperationException(
                    "Observed meal-incident owner contains duplicate occurrences.");
            }
            return occurrences.SingleOrDefault();
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private sealed class EmptyCharacterWorldQuery : ICharacterWorldQuery
    {
        internal static readonly EmptyCharacterWorldQuery Instance = new();
        public int CharacterVersion => 0;
        public IReadOnlyList<CharacterActor> Characters =>
            Array.Empty<CharacterActor>();
    }

    private sealed class EmptyStockQuery : IStockQuery
    {
        internal static readonly EmptyStockQuery Instance = new();
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            Array.Empty<WorldItemStackSnapshot>();
        public int GetGlobalQuantity(string itemDefinitionId) => 0;
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            string itemDefinitionId) => 0;
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            StockCategory category) => 0;
        public int GetWarehouseTotal(BuildingInstanceId warehouseId) => 0;
    }

    private sealed class EmptyAtomicItemConsumption :
        IAtomicItemConsumptionService,
        IIdempotentAtomicItemConsumptionService
    {
        internal static readonly EmptyAtomicItemConsumption Instance = new();
        public bool TryConsumeReserved(
            IReadOnlyList<ReservedItemConsumption> consumptions,
            string reservationOwnerId,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return consumptions == null || consumptions.Count == 0;
        }

        public bool TryConsumeReservedOnce(
            IReadOnlyList<ReservedItemConsumption> consumptions,
            string reservationOwnerId,
            out PhysicalEffectReceiptSaveData receipt,
            out DomainFailure failure)
        {
            receipt = null;
            if (consumptions == null || consumptions.Count == 0)
            {
                failure = DomainFailure.None;
                return true;
            }

            failure = new DomainFailure(
                FailureCode.ItemTransferConsumptionFailed,
                reservationOwnerId ?? string.Empty);
            return false;
        }
    }

    private sealed class AlwaysSatisfiedContentRequirements :
        IContentRequirementEvaluator
    {
        internal static readonly AlwaysSatisfiedContentRequirements Instance =
            new();
        public bool TryEvaluate(
            V20ContentRequirementSet requirements,
            RunMilestoneEvaluationSnapshot world,
            IReadOnlyList<string> participantCharacterIds,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }
        public bool IsLivingCharacterAtStage(
            CharacterId characterId,
            CharacterLifeStage lifeStage) => false;
    }

    private sealed class EmptySeasonalFeedTargetQuery :
        ISeasonalFeedSelfHeatingTargetQuery
    {
        internal static readonly EmptySeasonalFeedTargetQuery Instance = new();

        public bool TrySelect(
            SeasonalFeedSelfHeatingFireProfile profile,
            out SeasonalFeedSelfHeatingTarget target,
            out SeasonalFeedSelfHeatingTargetFailure reason)
        {
            target = default;
            reason = default;
            return false;
        }

        public bool TryRevalidate(
            in SeasonalFeedSelfHeatingTarget selected,
            SeasonalFeedSelfHeatingFireProfile profile,
            out SeasonalFeedSelfHeatingTarget current,
            out SeasonalFeedSelfHeatingTargetFailure reason)
        {
            current = default;
            reason = default;
            return false;
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

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
#endif
