#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class FacilityEvolutionDebugScenarios
{
    internal const string CausalFacilityEvolutionOwnerRestoreKind =
        "owner-family:facility-evolution-lineage";
    internal const string CausalFacilityEvolutionFirstActionId =
        "action:facility-evolution-stage-one";
    internal const string CausalFacilityEvolutionSecondActionId =
        "action:facility-evolution-stage-two";

    private const string CausalFacilityEvolutionOwnerKind =
        "owner-kind:facility-evolution-lineage";
    private const string CausalFacilityEvolutionStateSchema =
        "dungeonstory-causal-facility-evolution-owner-state@1";

    internal static IReadOnlyList<IProductionCausalActionFixture>
        CreateCausalEvolutionFixtures(
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
                "A facility-evolution checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new CausalEvolutionContext(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                CausalFacilityEvolutionFirstActionId,
                "facility",
                EvolutionOutcomeIds.FacilityEvolutionCompleted,
                context.CaptureState,
                context.ExecuteFirst,
                context.RequireFirstOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalFacilityEvolutionOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null,
                captureNarrativeEvidence: context.CaptureNarrativeEvidence),
            new DelegateProductionCausalActionFixture(
                CausalFacilityEvolutionSecondActionId,
                "facility",
                EvolutionOutcomeIds.FacilityEvolutionCompleted,
                () => new Dictionary<string, object>(),
                context.ExecuteSecond,
                context.RequireSecondOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalFacilityEvolutionOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null,
                captureNarrativeEvidence: context.CaptureNarrativeEvidence)
        };
    }

    internal static bool RunCausalEvolutionOwnerFamily(bool log = false)
    {
        const string OwnerId = "facility-evolution:causal-focused:primary";
        JObject firstCheckpoint;
        using (var fixtures = new CausalEvolutionFixtureSet(
                   CreateCausalEvolutionFixtures(
                       "run:causal-facility-evolution:first",
                       OwnerId)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                CausalFacilityEvolutionFirstActionId);
            IReadOnlyDictionary<string, object> before = first.CaptureState();
            Require((int)before["facility.evolutionStage"] == 0
                    && (int)before["facility.starGrade"] == 1
                    && (int)before["facility.historyCount"] == 0,
                "Facility evolution did not start from the exact authored state.");
            Require(first.Execute(),
                "The first real facility evolution did not execute.");
            Require(first.RequireExactOutcome().HasCanonicalPayloadHash,
                "The first facility evolution has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require((int)after["facility.evolutionStage"] == 1
                    && (int)after["facility.starGrade"] == 2
                    && (int)after["facility.historyCount"] == 1
                    && (int)after["facility.exactOutcomeCount"] == 1,
                "The first facility evolution did not publish its exact owner state.");
            firstCheckpoint = ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
        }

        JObject secondCheckpoint;
        using (var fixtures = new CausalEvolutionFixtureSet(
                   CreateCausalEvolutionFixtures(
                       "run:causal-facility-evolution:second",
                       OwnerId,
                       firstCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                CausalFacilityEvolutionFirstActionId);
            IProductionCausalActionFixture second = fixtures.Get(
                CausalFacilityEvolutionSecondActionId);
            IReadOnlyDictionary<string, object> restored = first.CaptureState();
            Require((int)restored["facility.evolutionStage"] == 1
                    && (int)restored["facility.starGrade"] == 2
                    && (int)restored["facility.historyCount"] == 1,
                "Fresh composition restore lost the first facility evolution.");
            Require(!first.Execute(),
                "The first facility evolution executed twice after restore.");
            Require(second.Execute(),
                "The second real facility evolution did not execute.");
            Require(second.RequireExactOutcome().HasCanonicalPayloadHash,
                "The second facility evolution has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require((int)after["facility.evolutionStage"] == 2
                    && (int)after["facility.starGrade"] == 3
                    && (int)after["facility.historyCount"] == 2
                    && (int)after["facility.exactOutcomeCount"] == 2,
                "The second facility evolution did not extend the same lineage.");
            secondCheckpoint = ((IProductionCausalOwnerCheckpointFixture)second)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new CausalEvolutionFixtureSet(
                   CreateCausalEvolutionFixtures(
                       "run:causal-facility-evolution:terminal",
                       OwnerId,
                       secondCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                CausalFacilityEvolutionFirstActionId);
            IProductionCausalActionFixture second = fixtures.Get(
                CausalFacilityEvolutionSecondActionId);
            IReadOnlyDictionary<string, object> restored = first.CaptureState();
            Require((int)restored["facility.evolutionStage"] == 2
                    && (int)restored["facility.starGrade"] == 3
                    && (int)restored["facility.historyCount"] == 2
                    && !first.Execute()
                    && !second.Execute(),
                "Terminal facility lineage was not idempotent after restore.");
        }

        JObject tampered = (JObject)secondCheckpoint.DeepClone();
        ((JObject)tampered["domainState"])["currentDefinitionId"] =
            "building:tampered-definition";
        bool tamperRejected = false;
        try
        {
            using var ignored = new CausalEvolutionFixtureSet(
                CreateCausalEvolutionFixtures(
                    "run:causal-facility-evolution:tampered",
                    OwnerId,
                    tampered));
        }
        catch (InvalidOperationException)
        {
            tamperRejected = true;
        }
        Require(tamperRejected,
            "A tampered facility definition identity was accepted.");

        if (log)
            Debug.Log("Facility evolution two-stage causal owner family PASS");
        return true;
    }

    public static void RunCausalEvolutionOwnerFamilyBatch()
    {
        try
        {
            Require(
                RunCausalEvolutionOwnerFamily(log: true),
                "Facility evolution causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private sealed class CausalEvolutionContext : IDisposable
    {
        private readonly string ownerId;
        private readonly string facilityPersistentId;
        private readonly EvolutionScenarioWorld world;
        private readonly BuildingSO eliteCombatResultData;
        private readonly FacilityEvolutionRecipeSO firstRecipe;
        private readonly FacilityEvolutionRecipeSO secondRecipe;
        private readonly EvolutionGameplayOutcomeEditorFixture outcomes;
        private readonly EditorFacilityUseFactFixture facilityUseFacts;
        private readonly FacilityEvolutionEngine engine;
        private readonly List<UnityEngine.Object> visitObjects = new();
        private BuildableObject facility;
        private int stage;
        private string firstOperationId = string.Empty;
        private long firstOwnerRevision;
        private string secondOperationId = string.Empty;
        private long secondOwnerRevision;
        private bool disposed;

        internal CausalEvolutionContext(
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
            facilityPersistentId =
                "building:causal-facility-evolution:" + scope;
            world = EvolutionScenarioWorld.CreateCombatDining();
            try
            {
                BindStableFacilityIdentity(world.SourceFacility);
                eliteCombatResultData = world.CreateBuildingData(
                    "정예 전투 식당",
                    FacilityRole.Meal | FacilityRole.Training);
                firstRecipe = CreateCombatRecipe(
                    world.SourceData,
                    world.CombatResultData,
                    consumeRecordToken: false);
                firstRecipe.requiredMaterials = Array.Empty<
                    FacilityEvolutionMaterialRequirement>();
                secondRecipe = CreateCombatRecipe(
                    world.CombatResultData,
                    eliteCombatResultData,
                    consumeRecordToken: false);
                secondRecipe.evolutionId =
                    "evolve_causal_elite_combat_dining";
                secondRecipe.displayName = "정예 전투 식당 진화";
                secondRecipe.requiredStarGrade = 2;
                secondRecipe.resultStarGrade = 3;
                secondRecipe.requiredMaterials = Array.Empty<
                    FacilityEvolutionMaterialRequirement>();
                world.TrackObject(firstRecipe);
                world.TrackObject(secondRecipe);
                PrepareFormulaExecutionFixture(
                    world,
                    firstRecipe,
                    secondRecipe);

                string ledgerRunId = HasPersistentOwner
                    ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                        this.ownerId,
                        CausalFacilityEvolutionOwnerKind)
                    : causalRunId + ":facility-evolution-ledger";
                outcomes = new EvolutionGameplayOutcomeEditorFixture(
                    ledgerRunId,
                    clockTime: 23f,
                    includeMigratedProducers: true,
                    includeFacilityEvolutionFacts: true);
                facilityUseFacts = new EditorFacilityUseFactFixture(
                    outcomes.MigratedProducerRuntime,
                    outcomes.Recorder,
                    outcomes.Ledger);
                engine = world.CreateEngine(
                    new StaticFacilityEvolutionRecipeProvider(
                        firstRecipe,
                        secondRecipe),
                    facts: outcomes.FacilityEvolutionFacts,
                    factRecovery: outcomes.FacilityEvolutionFacts,
                    calendar: new FixedFacilityEvolutionCalendar(
                        day: 23,
                        hour: 10),
                    outcomeEvidenceQuery:
                        new GameplayOutcomeNarrativeEvidenceQuery(
                            outcomes.Ledger),
                    evidenceUseTransaction:
                        new GameplayOutcomeEvidenceUseTransaction(
                            outcomes.Ledger,
                            new GameplayOutcomeNarrativeEvidenceQuery(
                                outcomes.Ledger)));
                facility = world.SourceFacility;

                if (checkpoint != null)
                {
                    JObject domain = ProductionCausalOwnerCheckpoint
                        .RestoreFamilyCommon(
                            checkpoint,
                            this.ownerId,
                            CausalFacilityEvolutionOwnerKind,
                            CausalFacilityEvolutionOwnerRestoreKind,
                            outcomes.Ledger);
                    RestoreDomainState(domain);
                }
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
            ? CausalFacilityEvolutionOwnerKind
            : string.Empty;
        internal string SourceFacilityPersistentId => facilityPersistentId;
        internal FacilityEvolutionRecipeSO SourceSecondRecipe => secondRecipe;
        internal BuildableObject SourceFacility => facility;
        internal FacilityEvolutionStateComponent SourceState => RequireState();
        internal string SourceDisplayName => CurrentDisplayName();
        internal int SourceStage => stage;

        internal void RecordFacilityVisits(int count, int nameOffset)
        {
            ThrowIfDisposed();
            if (stage != 1 || count < 1 || count > 4)
                throw new InvalidOperationException(
                    "Facility source visits require stage one and one to four visitors.");
            var publisher = new BuildingVisitEventPublisher(
                new GameEventBus(),
                new FixedGameSessionStateProvider(23),
                facilityUseFacts.Facts,
                facilityUseFacts.Facts);
            string[] names =
            {
                "연화", "무진", "설아", "태림", "해원",
                "도윤", "가람", "소명", "하린", "유담"
            };
            for (int index = 0; index < count; index++)
            {
                string displayName = names[Math.Abs(nameOffset + index)
                    % names.Length];
                CharacterSO data = CharacterAiEditorTestDependencies
                    .CreateCharacterFixtureData(
                        CharacterType.Customer,
                        displayName,
                        "human");
                GameObject actorObject = new(displayName);
                actorObject.AddComponent<SpriteRenderer>();
                CharacterAiEditorTestDependencies.EnsureCharacterProgression(
                    actorObject);
                CharacterActor actor = actorObject.AddComponent<CharacterActor>();
                CharacterAiEditorTestDependencies.Inject(actorObject);
                actor.Initialization(data);
                actor.characterType = CharacterType.Customer;
                actor.Identity.SetPersistentId(
                    "character:facility-evolution-visitor:"
                    + ProductionCausalOutcomeEvidence.Sha256Text(
                        ownerId + ":" + (nameOffset + index).ToString())
                        .Substring("sha256:".Length, 24));
                visitObjects.Add(actorObject);
                visitObjects.Add(data);
                publisher.CommitVisit(CharacterActor.From(actor), facility);
            }
            ValidateState();
        }

        internal bool CommitSecondFormulaSelection(
            FacilityFormulaEvolutionModuleSelectionDto response,
            out FacilityEvolutionResult result,
            out string failureReason)
        {
            ThrowIfDisposed();
            result = default;
            failureReason = string.Empty;
            if (stage != 1)
            {
                failureReason = "The second facility evolution is not pending.";
                return false;
            }
            if (!engine.TryCommitFormulaModuleSelection(
                    facility,
                    response,
                    out result,
                    out failureReason)
                || !result.Success
                || result.ResultBuilding == null)
            {
                if (string.IsNullOrWhiteSpace(failureReason))
                {
                    failureReason = string.IsNullOrWhiteSpace(result.Message)
                        ? $"Facility module selection returned success={result.Success}, "
                            + $"resultBuilding={(result.ResultBuilding == null ? "null" : "set")}."
                        : result.Message;
                }
                return false;
            }
            facility = result.ResultBuilding;
            world.SourceFacility = facility;
            secondOperationId = result.OutcomeOperationId;
            secondOwnerRevision = result.OutcomeOwnerRevision;
            stage = 2;
            ValidateState();
            return true;
        }

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            FacilityEvolutionStateComponent state = RequireState();
            FacilityEvolutionRecord record = state.GetRecord();
            return new Dictionary<string, object>
            {
                ["facility.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "facility-evolution:ephemeral",
                ["facility.instanceId"] = facilityPersistentId,
                ["facility.definitionId"] = CurrentDefinitionId(),
                ["facility.displayName"] = CurrentDisplayName(),
                ["facility.starGrade"] = state.StarGrade,
                ["facility.historyCount"] = state.EvolutionHistory.Count,
                ["facility.generation"] = state.InstanceEvolution.generation,
                ["facility.mercenaryTokenCount"] = record.GetToken(
                    FacilityEvolutionTerms.MercenaryHangout),
                ["facility.mutationTagCount"] = state.MutationTags.Count,
                ["facility.exactOutcomeCount"] = outcomes.Ledger
                    .CaptureGameplayOutcomes().exactOutcomes.Count,
                ["facility.evolutionStage"] = stage,
                ["facility.firstCompleted"] = stage >= 1,
                ["facility.secondCompleted"] = stage >= 2
            };
        }

        internal bool ExecuteFirst()
        {
            ThrowIfDisposed();
            if (stage != 0)
                return false;
            if (!TryExecuteFormulaEvolution(
                    engine,
                    facility,
                    firstRecipe,
                    out FacilityEvolutionResult result,
                    out string failureReason)
                || !result.Success
                || result.ResultBuilding == null)
            {
                throw new InvalidOperationException(
                    "First facility evolution execution failed: "
                    + (string.IsNullOrWhiteSpace(failureReason)
                        ? result.Message
                        : failureReason));
            }
            facility = result.ResultBuilding;
            world.SourceFacility = facility;
            firstOperationId = result.OutcomeOperationId;
            firstOwnerRevision = result.OutcomeOwnerRevision;
            stage = 1;
            ValidateState();
            return RequireFirstOutcome(false).HasCanonicalPayloadHash;
        }

        internal bool ExecuteSecond()
        {
            ThrowIfDisposed();
            if (stage != 1)
                return false;
            if (!TryExecuteFormulaEvolution(
                    engine,
                    facility,
                    secondRecipe,
                    out FacilityEvolutionResult result,
                    out _)
                || !result.Success
                || result.ResultBuilding == null)
            {
                return false;
            }
            facility = result.ResultBuilding;
            world.SourceFacility = facility;
            secondOperationId = result.OutcomeOperationId;
            secondOwnerRevision = result.OutcomeOwnerRevision;
            stage = 2;
            ValidateState();
            return RequireSecondOutcome(false).HasCanonicalPayloadHash;
        }

        internal GameplayOutcomeReplayIdentity RequireFirstOutcome() =>
            RequireFirstOutcome(true);

        internal GameplayOutcomeReplayIdentity RequireSecondOutcome() =>
            RequireSecondOutcome(true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(
                outcomes.Ledger,
                identity);

        internal IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot>
            CaptureNarrativeEvidence(
                GameplayEntityId subject,
                int maximumCount) =>
            ProductionCausalNarrativeEvidence.Capture(
                outcomes.Ledger,
                subject,
                maximumCount);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "This facility-evolution context has no persistent owner.");
            }
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                CausalFacilityEvolutionOwnerKind,
                CausalFacilityEvolutionOwnerRestoreKind,
                CaptureDomainState(),
                outcomes.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            for (int index = visitObjects.Count - 1; index >= 0; index--)
                if (visitObjects[index] != null)
                    UnityEngine.Object.DestroyImmediate(visitObjects[index]);
            facilityUseFacts?.Dispose();
            outcomes?.Dispose();
            world?.Dispose();
        }

        private void RestoreDomainState(JObject domain)
        {
            RequireFields(
                domain,
                "facility evolution domain state",
                "schemaVersion",
                "schema",
                "stage",
                "ownerId",
                "facilityPersistentId",
                "currentDefinitionId",
                "currentDisplayName",
                "currentStarGrade",
                "facilityState",
                "stateVersion",
                "statePayload",
                "firstOperationId",
                "firstOwnerRevision",
                "secondOperationId",
                "secondOwnerRevision");
            if ((int)domain["schemaVersion"] != 1
                || !string.Equals(
                    (string)domain["schema"],
                    CausalFacilityEvolutionStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)domain["ownerId"],
                    ownerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)domain["facilityPersistentId"],
                    facilityPersistentId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Facility evolution owner checkpoint identity is invalid.");
            }

            int restoredStage = (int)domain["stage"];
            if (restoredStage is < 0 or > 2)
                throw new InvalidOperationException(
                    "Facility evolution checkpoint stage is invalid.");
            BuildingSO target = restoredStage switch
            {
                0 => world.SourceData,
                1 => world.CombatResultData,
                2 => eliteCombatResultData,
                _ => throw new InvalidOperationException(
                    "Facility evolution checkpoint stage is unsupported.")
            };
            string expectedDefinitionId =
                FacilityEvolutionUtility.GetFacilityId(target);
            if (!string.Equals(
                    (string)domain["currentDefinitionId"],
                    expectedDefinitionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)domain["currentDisplayName"],
                    target.objectName,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Facility evolution checkpoint definition identity is invalid.");
            }

            if (restoredStage > 0)
            {
                GridFacilityEvolutionBuildingReplacer replacer =
                    world.CreateReplacer();
                if (!replacer.TryReplace(
                        facility,
                        target,
                        out BuildableObject replacement,
                        out string reason)
                    || replacement == null)
                {
                    throw new InvalidOperationException(
                        "Facility evolution checkpoint building restore failed: "
                        + reason);
                }
                facility = replacement;
                world.SourceFacility = replacement;
            }

            int stateVersion = (int)domain["stateVersion"];
            string statePayload = (string)domain["statePayload"];
            FacilityEvolutionStateComponent state = RequireState();
            if (!state.TryRestoreState(
                    stateVersion,
                    statePayload,
                    out string restoreError))
            {
                throw new InvalidOperationException(
                    "Facility evolution state restore failed: " + restoreError);
            }
            facility.SetFacilityLevel((int)domain["currentStarGrade"]);
            facility.RestoreFacilityState(JsonUtility.FromJson<
                FacilityRuntimeState>(
                ((JObject)domain["facilityState"]).ToString(
                    (Newtonsoft.Json.Formatting)0)));
            new FacilityEvolutionRecordComponentService(
                    new FacilityEvolutionRecordComponentFactory())
                .ReplaceWith(facility, state.GetRecord());

            stage = restoredStage;
            firstOperationId = (string)domain["firstOperationId"]
                ?? string.Empty;
            firstOwnerRevision = (long)domain["firstOwnerRevision"];
            secondOperationId = (string)domain["secondOperationId"]
                ?? string.Empty;
            secondOwnerRevision = (long)domain["secondOwnerRevision"];
            ValidateState();
            if (!JToken.DeepEquals(domain, CaptureDomainState()))
            {
                throw new InvalidOperationException(
                    "Facility evolution domain state changed during fresh restore.");
            }
        }

        private JObject CaptureDomainState()
        {
            FacilityEvolutionStateComponent state = RequireState();
            return new JObject
            {
                ["schemaVersion"] = 1,
                ["schema"] = CausalFacilityEvolutionStateSchema,
                ["stage"] = stage,
                ["ownerId"] = ownerId,
                ["facilityPersistentId"] = facilityPersistentId,
                ["currentDefinitionId"] = CurrentDefinitionId(),
                ["currentDisplayName"] = CurrentDisplayName(),
                ["currentStarGrade"] = state.StarGrade,
                ["facilityState"] = JObject.Parse(JsonUtility.ToJson(
                    facility.FacilityState.Clone())),
                ["stateVersion"] = state.CurrentVersion,
                ["statePayload"] = state.CaptureState(),
                ["firstOperationId"] = firstOperationId,
                ["firstOwnerRevision"] = firstOwnerRevision,
                ["secondOperationId"] = secondOperationId,
                ["secondOwnerRevision"] = secondOwnerRevision
            };
        }

        private void ValidateState()
        {
            ThrowIfDisposed();
            if (facility == null
                || !facility.PersistentInstanceId.IsValid
                || !string.Equals(
                    facility.PersistentInstanceId.Value,
                    facilityPersistentId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Facility evolution persistent building identity changed.");
            }
            FacilityEvolutionStateComponent state = RequireState();
            if (!string.Equals(
                    state.FacilityPersistentId,
                    facilityPersistentId,
                    StringComparison.Ordinal)
                || state.StarGrade != stage + 1
                || state.EvolutionHistory.Count != stage
                || state.HasPendingMaterialCommit
                || state.PendingFormulaPresentation != null && stage != 1)
            {
                throw new InvalidOperationException(
                    "Facility evolution state does not match its causal stage.");
            }
            int exactCount = outcomes.Ledger
                .CaptureGameplayOutcomes().exactOutcomes.Count(value =>
                    value != null
                    && string.Equals(
                        value.outcomeTypeId,
                        EvolutionOutcomeIds.FacilityEvolutionCompleted.Value,
                        StringComparison.Ordinal));
            if (exactCount != stage)
            {
                throw new InvalidOperationException(
                    "Facility evolution outcome count does not match its causal stage.");
            }
            if (stage == 0
                && (firstOperationId.Length != 0
                    || firstOwnerRevision != 0L
                    || secondOperationId.Length != 0
                    || secondOwnerRevision != 0L))
            {
                throw new InvalidOperationException(
                    "Initial facility evolution state contains terminal identities.");
            }
            if (stage >= 1)
                RequireFirstOutcome(false);
            if (stage >= 2)
                RequireSecondOutcome(false);
            if (stage < 2
                && (secondOperationId.Length != 0
                    || secondOwnerRevision != 0L))
            {
                throw new InvalidOperationException(
                    "Facility evolution second-stage identity appeared early.");
            }
        }

        private GameplayOutcomeReplayIdentity RequireFirstOutcome(
            bool throwOnFailure) => RequireExact(
                firstOperationId,
                firstOwnerRevision,
                throwOnFailure,
                "first");

        private GameplayOutcomeReplayIdentity RequireSecondOutcome(
            bool throwOnFailure) => RequireExact(
                secondOperationId,
                secondOwnerRevision,
                throwOnFailure,
                "second");

        private GameplayOutcomeReplayIdentity RequireExact(
            string operationId,
            long ownerRevision,
            bool throwOnFailure,
            string label)
        {
            if (string.IsNullOrWhiteSpace(operationId)
                || ownerRevision <= 0L)
            {
                if (throwOnFailure)
                {
                    throw new InvalidOperationException(
                        "The " + label
                        + " facility evolution identity is missing.");
                }
                return default;
            }
            var key = new GameplayResultKey(
                EvolutionOutcomeIds.FacilityProducerId,
                new GameplayOperationId(operationId),
                ownerRevision,
                0);
            if (outcomes.Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash)
            {
                return identity;
            }
            if (throwOnFailure)
            {
                throw new InvalidOperationException(
                    "The " + label
                    + " facility evolution exact outcome is missing.");
            }
            return default;
        }

        private void BindStableFacilityIdentity(BuildableObject value)
        {
            value.RestorePersistentIdentity(
                new BuildingInstanceId(facilityPersistentId));
            FacilityEvolutionStateComponent state = value
                .GetComponent<FacilityEvolutionStateComponent>();
            if (state == null)
            {
                throw new InvalidOperationException(
                    "Facility evolution state component is missing.");
            }
            FacilityEvolutionState evolution = state.InstanceEvolution;
            evolution.facilityPersistentId = facilityPersistentId;
            state.ReplaceInstanceEvolution(evolution);
        }

        private FacilityEvolutionStateComponent RequireState()
        {
            FacilityEvolutionStateComponent state = facility != null
                ? facility.GetComponent<FacilityEvolutionStateComponent>()
                : null;
            return state ?? throw new InvalidOperationException(
                "Facility evolution state component is missing.");
        }

        private string CurrentDefinitionId() =>
            FacilityEvolutionUtility.GetFacilityId(facility.BuildingData);

        private string CurrentDisplayName() =>
            facility?.BuildingData?.objectName ?? string.Empty;

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(CausalEvolutionContext));
        }
    }

    private sealed class CausalEvolutionFixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal CausalEvolutionFixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> fixtures)
        {
            this.fixtures = fixtures
                ?? throw new ArgumentNullException(nameof(fixtures));
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
        params string[] expected)
    {
        if (value == null)
            throw new InvalidOperationException(label + " is missing.");
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

    private static void Require(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }
}
#endif
