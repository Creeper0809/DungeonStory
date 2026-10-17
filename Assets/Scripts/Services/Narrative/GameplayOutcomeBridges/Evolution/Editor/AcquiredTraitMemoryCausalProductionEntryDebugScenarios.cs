#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class AcquiredTraitMemoryCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:acquired-trait-memory-erasure";
    internal const string ManifestActionId =
        "action:trait-acquired-inference-complete";
    internal const string ReactionActionId =
        "action:trait-acquired-reaction-execute";
    internal const string EraseActionId =
        "action:trait-memory-erasure-terminal";

    private const string OwnerKind =
        "owner-kind:acquired-trait-memory-erasure";
    private const string DomainStateSchema =
        "dungeonstory-causal-acquired-trait-memory-owner-state@2";
    private const string ReactionModuleId =
        "acquired-trait:module:steady-hands";
    private const string ReactionId =
        "acquired-trait:reaction:finished-work-insight";
    private const int AbsoluteDay = 31;
    private static readonly string[] EvidenceFactIds =
    {
        "work:causal-trait-craft-a",
        "work:causal-trait-craft-b",
        "work:causal-trait-craft-c"
    };

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
                "An acquired-trait owner checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(3, context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                ManifestActionId,
                "character-growth",
                EvolutionOutcomeIds.TraitInferenceCompleted,
                context.CaptureState,
                context.Manifest,
                context.RequireManifestOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                ReactionActionId,
                "character-growth",
                EvolutionOutcomeIds.TraitReactionExecuted,
                () => new Dictionary<string, object>(),
                context.React,
                context.RequireReactionOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                EraseActionId,
                "character-growth",
                EvolutionOutcomeIds.MemoryErasureTerminal,
                () => new Dictionary<string, object>(),
                context.Erase,
                context.RequireEraseOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    public static bool RunAll(bool log = false)
    {
        const string OwnerIdValue = "owner:causal-acquired-trait-memory:focused";
        JObject manifestedCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-acquired-trait-memory:manifest",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture manifest = fixtures.Get(ManifestActionId);
            Require(manifest.Execute(), "The real acquired-trait completion did not execute.");
            GameplayOutcomeReplayIdentity identity = manifest.RequireExactOutcome();
            Require(identity.HasCanonicalPayloadHash,
                "The acquired-trait completion has no canonical payload hash.");
            IReadOnlyDictionary<string, object> state = manifest.CaptureState();
            Require((bool)state["trait.manifested"]
                    && !(bool)state["trait.erased"]
                    && (int)state["trait.activeCount"] == 1
                    && (int)state["trait.sealQuantity"] == 1,
                "Manifestation did not retain one active trait and one physical seal.");
            manifestedCheckpoint = ((IProductionCausalOwnerCheckpointFixture)manifest)
                .CaptureOwnerCheckpoint();
        }

        JObject reactionCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-acquired-trait-memory:reaction",
                   OwnerIdValue,
                   manifestedCheckpoint)))
        {
            IProductionCausalActionFixture reaction = fixtures.Get(ReactionActionId);
            Require(reaction.Execute(),
                "The real acquired-trait special reaction did not execute.");
            GameplayOutcomeReplayIdentity identity = reaction.RequireExactOutcome();
            Require(identity.HasCanonicalPayloadHash,
                "The acquired-trait reaction has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = fixtures
                .Get(ManifestActionId).CaptureState();
            Require((bool)after["trait.reactionExecuted"]
                    && (int)after["trait.reactionTriggerCount"] == 1
                    && (int)after["trait.currentExperience"] > 0,
                "The special reaction did not persist its trigger and XP reward.");
            Require(!reaction.Execute(),
                "The bounded special reaction executed twice on the same day.");
            reactionCheckpoint = ((IProductionCausalOwnerCheckpointFixture)reaction)
                .CaptureOwnerCheckpoint();
        }

        JObject erasedCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-acquired-trait-memory:erase",
                   OwnerIdValue,
                   reactionCheckpoint)))
        {
            IProductionCausalActionFixture erase = fixtures.Get(EraseActionId);
            IReadOnlyDictionary<string, object> before = fixtures
                .Get(ManifestActionId).CaptureState();
            int factCount = (int)before["trait.narrativeFactCount"];
            Require(erase.Execute(), "The real memory-erasure transaction did not execute.");
            GameplayOutcomeReplayIdentity identity = erase.RequireExactOutcome();
            Require(identity.HasCanonicalPayloadHash,
                "The memory-erasure outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = fixtures
                .Get(ManifestActionId).CaptureState();
            Require((bool)after["trait.manifested"]
                    && (bool)after["trait.reactionExecuted"]
                    && (int)after["trait.reactionTriggerCount"] == 1
                    && (bool)after["trait.erased"]
                    && (int)after["trait.activeCount"] == 0
                    && (int)after["trait.sealQuantity"] == 0
                    && (int)after["trait.narrativeFactCount"] == factCount,
                "Memory erasure did not remove only the selected effect and seal.");
            erasedCheckpoint = ((IProductionCausalOwnerCheckpointFixture)erase)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-acquired-trait-memory:final-restore",
                   OwnerIdValue,
                   erasedCheckpoint)))
        {
            IReadOnlyDictionary<string, object> restored = fixtures
                .Get(ManifestActionId).CaptureState();
            Require((bool)restored["trait.erased"]
                    && (bool)restored["trait.reactionExecuted"]
                    && (int)restored["trait.reactionTriggerCount"] == 1
                    && (int)restored["trait.activeCount"] == 0
                    && (int)restored["trait.sealQuantity"] == 0,
                "The erased trait or consumed seal reappeared after fresh restore.");
            Require(!fixtures.Get(EraseActionId).Execute(),
                "A restored terminal erasure executed twice.");
        }

        JObject tampered = (JObject)erasedCheckpoint.DeepClone();
        ((JObject)tampered["domainState"])["traitInstanceId"] =
            "acquired-trait-instance:tampered";
        bool tamperRejected = false;
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-acquired-trait-memory:tampered",
                OwnerIdValue,
                tampered));
        }
        catch (InvalidOperationException)
        {
            tamperRejected = true;
        }
        Require(tamperRejected,
            "A tampered acquired-trait instance identity was accepted.");

        if (log)
            Debug.Log(
                "Acquired-trait manifestation -> special reaction -> memory erasure causal owner family PASS");
        return true;
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly string characterId;
        private readonly string requestId;
        private readonly string requestKey;
        private readonly string erasureOperationId;
        private readonly FixedClock clock;
        private readonly GameEventBus events;
        private readonly GameplayOutcomeLedger ledger;
        private readonly DomainFactDispatcher traitInferenceFacts;
        private readonly IDisposable traitInferenceBinding;
        private readonly CharacterTraitInferencePublicationRuntime traitInferences;
        private readonly DomainFactDispatcher traitReactionFacts;
        private readonly IDisposable traitReactionBinding;
        private readonly CharacterTraitReactionPublicationRuntime traitReactions;
        private readonly DomainFactDispatcher memoryErasureFacts;
        private readonly IDisposable memoryErasureBinding;
        private readonly MemoryErasurePublicationRuntime memoryErasures;
        private readonly CharacterAcquiredTraitSettingsSO settings;
        private readonly CharacterAcquiredTraitModuleSO[] modules;
        private readonly CharacterAcquiredTraitInferenceService inference;
        private readonly GameObject actorObject;
        private readonly CharacterSO actorData;
        private readonly CharacterActor actor;
        private readonly CharacterCarryInventory carry;
        private readonly CharacterAcquiredTraitSpecialReactionRuntime reactionRuntime;
        private readonly WorldItemRepository repository;
        private readonly PhysicalItemBatchDispositionService disposition;
        private readonly string sealStackId;
        private bool manifested;
        private bool reactionExecuted;
        private bool erased;
        private string traitInstanceId = string.Empty;
        private string traitCombinationId = string.Empty;
        private string traitDisplayName = string.Empty;
        private string traitModuleIds = string.Empty;
        private string traitEvidenceFactIds = string.Empty;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string stableSeed = HasPersistentOwner ? this.ownerId : causalRunId;
            string scope = ProductionCausalOutcomeEvidence.Sha256Text(stableSeed)
                .Substring("sha256:".Length, 24);
            characterId = "character:causal-acquired-trait:" + scope;
            requestId = "request:causal-acquired-trait:" + scope;
            requestKey = "request-key:causal-acquired-trait:" + scope;
            erasureOperationId = "operation:causal-memory-erasure:" + scope;

            clock = new FixedClock(AbsoluteDay, 8);
            events = new GameEventBus();
            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new AcquiredTraitInferenceOutcomeDescriptor(),
                    new AcquiredTraitReactionOutcomeDescriptor(),
                    new MemoryErasureOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new AcquiredTraitInferenceOutcomeAdapter(),
                    new AcquiredTraitReactionOutcomeAdapter(),
                    new MemoryErasureOutcomeAdapter()
                });
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(this.ownerId, OwnerKind)
                : causalRunId + ":trait-ledger";
            ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(16, 0, 128),
                new GameplayOutcomeRunId(ledgerRunId),
                1L);
            GameplayOutcomeRecorder recorder = new(ledger, registry, events);
            traitInferenceFacts = new DomainFactDispatcher();
            traitInferenceBinding = CharacterTraitInferredFactMapping.CreateBinding(
                    registry,
                    ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, ledger))
                .Connect(traitInferenceFacts);
            traitInferences = new CharacterTraitInferencePublicationRuntime(
                traitInferenceFacts,
                traitInferenceFacts);
            traitReactionFacts = new DomainFactDispatcher();
            traitReactionBinding = CharacterTraitReactionFactMapping.CreateBinding(
                    registry,
                    ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, ledger))
                .Connect(traitReactionFacts);
            traitReactions = new CharacterTraitReactionPublicationRuntime(
                traitReactionFacts,
                traitReactionFacts);
            memoryErasureFacts = new DomainFactDispatcher();
            memoryErasureBinding = MemoryErasureTerminalFactMapping.CreateBinding(
                    registry,
                    ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, ledger))
                .Connect(memoryErasureFacts);
            memoryErasures = new MemoryErasurePublicationRuntime(
                memoryErasureFacts,
                memoryErasureFacts);

            settings = AssetDatabase.LoadAssetAtPath<CharacterAcquiredTraitSettingsSO>(
                V25AcquiredTraitContentAssetBuilder.SettingsPath);
            modules = AssetDatabase.FindAssets(
                    "t:CharacterAcquiredTraitModuleSO",
                    new[] { V25AcquiredTraitContentAssetBuilder.ModuleRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterAcquiredTraitModuleSO>)
                .Where(value => value != null)
                .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
                .ToArray();
            if (settings == null || modules.Length == 0)
                throw new InvalidOperationException("Authored acquired-trait content is missing.");
            inference = new CharacterAcquiredTraitInferenceService(settings, modules);

            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "연화",
                "human");
            actorData.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();
            actorObject = new GameObject("AcquiredTraitMemoryCausal_" + scope);
            actorObject.AddComponent<SpriteRenderer>();
            actor = actorObject.AddComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.Identity.SetPersistentId(characterId);
            actor.Initialization(actorData);
            actor.Progression.ApplyPreparedIdentity(
                "연화",
                "causal-acquired-trait-memory",
                Array.Empty<int>(),
                CharacterPotentialGrade.Ordinary,
                7331,
                autoChooseDrafts: false,
                maximumTraitCount: 4,
                ensureInitialDrafts: false);

            CharacterMoodPolicyService moodPolicy = new(
                new CharacterIdentityRuleRouter(),
                new CharacterPersistentNeedRuntime(
                    new CharacterIdentityStateStore(),
                    clock));
            reactionRuntime = new CharacterAcquiredTraitSpecialReactionRuntime(
                new AcquiredTraitDefinitionSource(settings, modules),
                new FixedCharacterWorld(actor),
                events,
                moodPolicy,
                traitReactionFacts,
                traitReactions);
            reactionRuntime.Start();

            repository = new WorldItemRepository(
                new DeterministicIds(scope),
                new DungeonRuntimeAggregateRootStore());
            IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
            IPhysicalItemMassQuery mass = new PhysicalItemMassQuery(catalog);
            ItemQuantityReservationService reservations = new(
                repository,
                EditorNullItemMarkerPresenter.Instance,
                clock);
            disposition = new PhysicalItemBatchDispositionService(
                repository,
                mass,
                EditorNullItemMarkerPresenter.Instance,
                reservations);
            sealStackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                MemoryErasureSealItemRules.ItemId,
                MemoryErasureSealItemRules.UseQuantity,
                WorldItemStackState.Carried,
                destinationId: "character-carry:" + characterId);
            carry = actorObject.GetComponent<CharacterCarryInventory>()
                ?? actorObject.AddComponent<CharacterCarryInventory>();
            carry.Restore(CreateCarrySnapshot(sealStackId, erasureOperationId));

            try
            {
                if (checkpoint == null)
                    SeedNarrativeEvidence();
                else
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
        internal string PersistentOwnerKind => HasPersistentOwner ? OwnerKind : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            CharacterAcquiredTraitAggregateState state = actor.Progression
                .CaptureAcquiredTraitState();
            return new Dictionary<string, object>
            {
                ["trait.ownerId"] = HasPersistentOwner ? ownerId : "owner:ephemeral-trait",
                ["trait.characterId"] = characterId,
                ["trait.revision"] = state.revision,
                ["trait.activeCount"] = state.ActiveCount,
                ["trait.narrativeFactCount"] = actor.Progression.NarrativeLedger.facts.Count,
                ["trait.sealQuantity"] = repository.GetEditorTestQuantity(sealStackId),
                ["trait.manifested"] = manifested,
                ["trait.reactionExecuted"] = reactionExecuted,
                ["trait.reactionTriggerCount"] = ReactionTriggerCount(state),
                ["trait.erased"] = erased,
                ["trait.instanceId"] = traitInstanceId,
                ["trait.level"] = actor.Progression.Level,
                ["trait.currentExperience"] = actor.Progression.CurrentExperience
            };
        }

        internal bool Manifest()
        {
            ThrowIfDisposed();
            if (manifested || erased)
                return false;
            int expectedRevision = actor.Progression.AcquiredTraitRevision;
            IReadOnlyList<string> projected = ProjectEvidence();
            CharacterAcquiredTraitInferenceCommandResult submission =
                inference.SubmitMilestone(
                    actor.Progression,
                    new CharacterAcquiredTraitSubmissionCommand(
                        characterId,
                        requestId,
                        requestKey,
                        3,
                        projected,
                        expectedRevision,
                        NarrativeInferenceTimestamp.FromGameTick(3L)));
            if (!submission.Succeeded || submission.Packet == null)
                return false;

            CharacterAcquiredTraitAggregateState submitted = actor.Progression
                .CaptureAcquiredTraitState();
            if (!submitted.TryGetPendingRequest(
                    requestId,
                    out CharacterAcquiredTraitPendingRequestState pending)
                || pending.presentationState
                    != CharacterAcquiredTraitPresentationState.ModuleSelectionPending
                || pending.offeredBenefitModuleIds.Count == 0
                || pending.evidenceFactIds.Count == 0)
            {
                return false;
            }
            if (!pending.offeredBenefitModuleIds.Contains(
                    ReactionModuleId,
                    StringComparer.Ordinal))
            {
                return false;
            }
            string response = JsonUtility.ToJson(
                new CharacterAcquiredTraitModuleSelectionResponseDto
                {
                    selectionId = pending.moduleSelectionId,
                    positiveModuleIds = new List<string>
                    {
                        ReactionModuleId
                    },
                    drawbackModuleIds = new List<string>(),
                    evidenceFactIds = new List<string>
                    {
                        pending.evidenceFactIds[0]
                    },
                    displayName = "거듭된 손길",
                    narrativeFlavor = "반복된 제작 경험이 흔들리지 않는 손놀림으로 발현되었다."
                });
            CharacterAcquiredTraitInferenceCommandResult completion =
                inference.CompleteMilestone(
                    actor.Progression,
                    new CharacterAcquiredTraitCompletionCommand(
                        characterId,
                        requestId,
                        requestKey,
                        submission.Packet.candidatePacketHash,
                        expectedRevision + 1,
                        expectedRevision + 1,
                        response,
                        NarrativeInferenceTimestamp.FromGameTick(4L)),
                    submission.Packet,
                    traitInferenceFacts,
                    traitInferences);
            if (!completion.Succeeded)
                return false;
            CharacterAcquiredTraitInstanceState active = actor.Progression
                .CaptureAcquiredTraitState()
                .CaptureActiveInstances()
                .SingleOrDefault();
            traitInstanceId = active?.instanceId ?? string.Empty;
            traitCombinationId = active == null
                ? string.Empty
                : string.IsNullOrWhiteSpace(active.combinationId)
                    ? CharacterAcquiredTraitCombinationIdentity.Build(active.moduleIds)
                    : active.combinationId;
            traitDisplayName = active?.displayName ?? string.Empty;
            traitModuleIds = string.Join("|", (active?.moduleIds ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal));
            traitEvidenceFactIds = string.Join("|", (active?.evidenceFactIds
                    ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal));
            GameplayOutcomeReplayIdentity manifestedOutcome = RequireManifestOutcome(false);
            manifested = traitInstanceId.Length > 0
                && manifestedOutcome.HasCanonicalPayloadHash
                && HasFact(manifestedOutcome,
                    EvolutionOutcomeIds.TraitInstanceFact, traitInstanceId)
                && HasFact(manifestedOutcome,
                    EvolutionOutcomeIds.TraitCombinationFact, traitCombinationId)
                && HasFact(manifestedOutcome,
                    EvolutionOutcomeIds.TraitDisplayNameFact, traitDisplayName)
                && HasFact(manifestedOutcome,
                    EvolutionOutcomeIds.TraitModuleIdsFact, traitModuleIds)
                && HasFact(manifestedOutcome,
                    EvolutionOutcomeIds.TraitEvidenceFactIdsFact,
                    traitEvidenceFactIds);
            return manifested;
        }

        internal bool React()
        {
            ThrowIfDisposed();
            if (!manifested || reactionExecuted || erased || traitInstanceId.Length == 0)
                return false;
            CharacterAcquiredTraitAggregateState before = actor.Progression
                .CaptureAcquiredTraitState();
            int experienceBefore = actor.Progression.CurrentExperience;
            events.Publish(new WorkCompletedIdentityEvent(
                actor.BuildingCharacterId,
                "work:causal-trait-reaction",
                "item:causal-trait-product",
                CharacterCommandOrigin.DirectPlayerOrder,
                AbsoluteDay));
            CharacterAcquiredTraitAggregateState after = actor.Progression
                .CaptureAcquiredTraitState();
            reactionExecuted = after.revision == before.revision + 1
                && ReactionTriggerCount(after) == 1
                && actor.Progression.CurrentExperience > experienceBefore
                && RequireReactionOutcome(false).HasCanonicalPayloadHash;
            return reactionExecuted;
        }

        internal bool Erase()
        {
            ThrowIfDisposed();
            if (!manifested || !reactionExecuted || erased || traitInstanceId.Length == 0)
                return false;
            CharacterAcquiredTraitAggregateState acquiredBeforeErase = actor.Progression
                .CaptureAcquiredTraitState();
            if (!acquiredBeforeErase.TryGetInstance(
                    traitInstanceId,
                    out CharacterAcquiredTraitInstanceState selectedBeforeErase))
            {
                throw new InvalidOperationException(
                    "The acquired-trait instance disappeared before memory erasure.");
            }
            traitCombinationId = string.IsNullOrWhiteSpace(
                selectedBeforeErase.combinationId)
                ? CharacterAcquiredTraitCombinationIdentity.Build(
                    selectedBeforeErase.moduleIds)
                : selectedBeforeErase.combinationId;
            traitDisplayName = selectedBeforeErase.displayName;
            traitModuleIds = string.Join("|", (selectedBeforeErase.moduleIds
                    ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal));
            var service = new MemoryErasureSealTransactionService(
                CharacterAiEditorTestDependencies.ContentDefinitions,
                clock,
                disposition,
                memoryErasureFacts,
                memoryErasures);
            MemoryErasureSealUseResult result = service.TryErase(
                actor,
                traitInstanceId,
                carry,
                sealStackId,
                erasureOperationId);
            if (result.Status != MemoryErasureSealUseStatus.Succeeded)
            {
                throw new InvalidOperationException(
                    "Memory erasure failed: " + result.Status + " / " + result.Detail);
            }
            GameplayOutcomeReplayIdentity eraseOutcome = RequireEraseOutcome(false);
            erased = repository.GetEditorTestQuantity(sealStackId) == 0
                && carry.CountItem(MemoryErasureSealItemRules.ItemId) == 0
                && eraseOutcome.HasCanonicalPayloadHash
                && HasFact(eraseOutcome,
                    EvolutionOutcomeIds.TraitInstanceFact, traitInstanceId)
                && HasFact(eraseOutcome,
                    EvolutionOutcomeIds.TraitCombinationFact, traitCombinationId)
                && HasFact(eraseOutcome,
                    EvolutionOutcomeIds.TraitDisplayNameFact, traitDisplayName)
                && HasFact(eraseOutcome,
                    EvolutionOutcomeIds.TraitModuleIdsFact, traitModuleIds);
            if (!erased)
            {
                string actualFacts = ledger.TryGetExact(
                        eraseOutcome.OutcomeId,
                        out GameplayOutcomeSnapshot exact)
                    ? string.Join(",", (exact.facts ?? new List<GameplayOutcomeFactSnapshot>())
                        .Select(value => (value?.factId ?? "<null>")
                            + "=" + (value?.value ?? "<null>")))
                    : "<exact-outcome-missing>";
                throw new InvalidOperationException(
                    "Memory erasure committed but its physical state or typed outcome facts are incomplete. "
                    + "repo=" + repository.GetEditorTestQuantity(sealStackId)
                    + ",carry=" + carry.CountItem(MemoryErasureSealItemRules.ItemId)
                    + ",identity=" + eraseOutcome.HasCanonicalPayloadHash
                    + ",expected=" + traitInstanceId + "/" + traitCombinationId
                    + "/" + traitDisplayName + "/" + traitModuleIds
                    + ",facts=" + actualFacts);
            }
            return erased;
        }

        internal GameplayOutcomeReplayIdentity RequireManifestOutcome() =>
            RequireManifestOutcome(true);

        internal GameplayOutcomeReplayIdentity RequireReactionOutcome() =>
            RequireReactionOutcome(true);

        internal GameplayOutcomeReplayIdentity RequireEraseOutcome() =>
            RequireEraseOutcome(true);

        internal JObject CaptureOutcomeEvidence(GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
                throw new InvalidOperationException("This trait context has no persistent owner.");
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = DomainStateSchema,
                    ["characterId"] = characterId,
                    ["requestId"] = requestId,
                    ["requestKey"] = requestKey,
                    ["erasureOperationId"] = erasureOperationId,
                    ["sealStackId"] = sealStackId,
                    ["physicalSealQuantity"] = repository.GetEditorTestQuantity(sealStackId),
                    ["progression"] = Serialize(CaptureProgression(), "progression"),
                    ["carry"] = Serialize(carry.Capture(), "carry"),
                    ["traitInferencePublications"] = Serialize(
                        traitInferences.Capture(),
                        "traitInferencePublications"),
                    ["traitReactionPublications"] = Serialize(
                        traitReactions.Capture(),
                        "traitReactionPublications"),
                    ["memoryErasurePublications"] =
                        CaptureMemoryErasurePublications(),
                    ["manifested"] = manifested,
                    ["reactionExecuted"] = reactionExecuted,
                    ["erased"] = erased,
                    ["traitInstanceId"] = traitInstanceId
                },
                ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            reactionRuntime?.Dispose();
            memoryErasureBinding?.Dispose();
            memoryErasureFacts?.Dispose();
            traitReactionBinding?.Dispose();
            traitReactionFacts?.Dispose();
            traitInferenceBinding?.Dispose();
            traitInferenceFacts?.Dispose();
            if (actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
            if (actorData != null)
                UnityEngine.Object.DestroyImmediate(actorData);
        }

        private void SeedNarrativeEvidence()
        {
            foreach (string factId in EvidenceFactIds)
            {
                actor.Progression.RecordNarrative(
                    CharacterNarrativeDomain.Work,
                    factId,
                    "facility:causal-workshop",
                    "completed",
                    1f,
                    1,
                    triggerPassives: false);
            }
            if (CharacterAcquiredTraitExperienceScore.Require(
                    actor.Progression.NarrativeLedger) != 3)
            {
                throw new InvalidOperationException(
                    "The causal trait evidence did not reach the first manifestation gate.");
            }
        }

        private IReadOnlyList<string> ProjectEvidence()
        {
            NarrativePublicContextMaterial publicMaterial =
                CharacterAcquiredTraitPromptBuilder.BuildPublicMaterialForLedgerEvidence(
                    NarrativeRequestContextTestFixture.CreateEmpty(), actor.Progression,
                    EvidenceFactIds);
            return CharacterAcquiredTraitPromptBuilder.BuildProjectedEvidenceFactIds(
                publicMaterial,
                EvidenceFactIds);
        }

        private GameplayOutcomeReplayIdentity RequireManifestOutcome(bool throwOnFailure)
        {
            var key = new GameplayResultKey(
                EvolutionOutcomeIds.TraitInferenceProducerId,
                new GameplayOperationId(
                    "trait-inference:" + characterId + ":revision:00000002"),
                2L,
                0);
            return RequireExact(key, EvolutionOutcomeIds.TraitInferenceCompleted, throwOnFailure);
        }

        private GameplayOutcomeReplayIdentity RequireEraseOutcome(bool throwOnFailure)
        {
            MemoryErasurePublicationData[] matches = memoryErasures.Capture()
                .observations
                .Where(value => value?.fact != null
                    && string.Equals(
                        value.fact.operationId,
                        erasureOperationId,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                if (throwOnFailure)
                {
                    throw new InvalidOperationException(
                        "Causal memory-erasure publication journal has no exact terminal fact.");
                }
                return default;
            }
            return RequireExact(
                MemoryErasureTerminalFactMapping.ResultKey(matches[0].fact),
                EvolutionOutcomeIds.MemoryErasureTerminal,
                throwOnFailure);
        }

        private GameplayOutcomeReplayIdentity RequireReactionOutcome(bool throwOnFailure)
        {
            var key = new GameplayResultKey(
                EvolutionOutcomeIds.TraitReactionProducerId,
                new GameplayOperationId(
                    "trait-reaction:" + characterId + ":revision:00000003"),
                3L,
                0);
            return RequireExact(
                key,
                EvolutionOutcomeIds.TraitReactionExecuted,
                throwOnFailure);
        }

        private GameplayOutcomeReplayIdentity RequireExact(
            GameplayResultKey key,
            GameplayOutcomeTypeId expectedType,
            bool throwOnFailure)
        {
            bool valid = ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot exact)
                && exact != null
                && string.Equals(exact.outcomeTypeId, expectedType.Value, StringComparison.Ordinal);
            if (!valid && throwOnFailure)
            {
                throw new InvalidOperationException(
                    "Causal acquired-trait production entry did not publish exact outcome "
                    + expectedType.Value + ".");
            }
            return valid ? identity : default;
        }

        private bool HasFact(
            GameplayOutcomeReplayIdentity identity,
            GameplayOutcomeFactId factId,
            string expectedValue) => identity.HasCanonicalPayloadHash
            && ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot exact)
            && exact?.facts != null
            && exact.facts.Any(value => value != null
                && string.Equals(value.factId, factId.Value, StringComparison.Ordinal)
                && string.Equals(value.value, expectedValue, StringComparison.Ordinal));

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
                "causal acquired-trait owner state",
                "schemaVersion",
                "schema",
                "characterId",
                "requestId",
                "requestKey",
                "erasureOperationId",
                "sealStackId",
                "physicalSealQuantity",
                "progression",
                "carry",
                "traitInferencePublications",
                "traitReactionPublications",
                "memoryErasurePublications",
                "manifested",
                "reactionExecuted",
                "erased",
                "traitInstanceId");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals((string)state["schema"], DomainStateSchema, StringComparison.Ordinal)
                || !string.Equals((string)state["characterId"], characterId, StringComparison.Ordinal)
                || !string.Equals((string)state["requestId"], requestId, StringComparison.Ordinal)
                || !string.Equals((string)state["requestKey"], requestKey, StringComparison.Ordinal)
                || !string.Equals((string)state["erasureOperationId"], erasureOperationId, StringComparison.Ordinal)
                || !string.Equals((string)state["sealStackId"], sealStackId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Causal acquired-trait owner identity or operation IDs changed.");
            }

            ProgressionEnvelope progression = Deserialize<ProgressionEnvelope>(
                state["progression"],
                "progression");
            actor.Progression.RestorePersistentState(new CharacterProgressionSnapshot(
                progression.level,
                progression.currentExperience,
                progression.growthState,
                progression.narrativeLedger,
                progression.acquiredTraitState));
            CharacterTraitInferencePublicationSaveData savedPublications =
                CharacterTraitInferencePublicationRuntime.ValidateAndClone(
                    Deserialize<CharacterTraitInferencePublicationSaveData>(
                        state["traitInferencePublications"],
                        "traitInferencePublications"));
            foreach (CharacterTraitInferencePublicationData observation
                in savedPublications.observations)
            {
                CharacterTraitInferredFactPreflight.ValidateObservation(
                    observation,
                    ledger.CaptureGameplayOutcomes());
            }
            traitInferences.Restore(savedPublications);
            CharacterTraitReactionPublicationSaveData savedReactionPublications =
                CharacterTraitReactionPublicationRuntime.ValidateAndClone(
                    Deserialize<CharacterTraitReactionPublicationSaveData>(
                        state["traitReactionPublications"],
                        "traitReactionPublications"));
            foreach (CharacterTraitReactionPublicationData observation
                in savedReactionPublications.observations)
            {
                CharacterTraitReactionFactPreflight.ValidateObservation(
                    observation,
                    ledger.CaptureGameplayOutcomes());
            }
            traitReactions.Restore(savedReactionPublications);
            string memoryErasureJson = state["memoryErasurePublications"]
                .ToString(Newtonsoft.Json.Formatting.None);
            MemoryErasurePublicationSaveSection.ValidateJson(memoryErasureJson);
            MemoryErasurePublicationSaveData savedMemoryErasures =
                MemoryErasurePublicationRuntime.ValidateAndClone(
                    DungeonSaveJson.Deserialize<MemoryErasurePublicationSaveData>(
                        memoryErasureJson));
            foreach (MemoryErasurePublicationData observation
                in savedMemoryErasures.observations)
            {
                MemoryErasureTerminalFactPreflight.ValidateObservation(
                    observation,
                    ledger.CaptureGameplayOutcomes());
            }
            memoryErasures.Restore(savedMemoryErasures);
            carry.Restore(Deserialize<CharacterCarryInventorySaveData>(state["carry"], "carry"));
            int physicalQuantity = (int)state["physicalSealQuantity"];
            if (physicalQuantity == 0)
                WorldItemRepositoryEditorAccess.TryRemoveStack(repository, sealStackId);
            else if (physicalQuantity != MemoryErasureSealItemRules.UseQuantity)
                throw new InvalidOperationException("Causal trait checkpoint has invalid seal quantity.");
            manifested = (bool)state["manifested"];
            reactionExecuted = (bool)state["reactionExecuted"];
            erased = (bool)state["erased"];
            traitInstanceId = (string)state["traitInstanceId"] ?? string.Empty;
        }

        private ProgressionEnvelope CaptureProgression()
        {
            CharacterProgressionSnapshot snapshot = actor.Progression.CapturePersistentState();
            return new ProgressionEnvelope
            {
                level = snapshot.Level,
                currentExperience = snapshot.CurrentExperience,
                growthState = snapshot.GrowthState.Clone(),
                narrativeLedger = snapshot.NarrativeLedger.Clone(),
                acquiredTraitState = snapshot.AcquiredTraitState.Clone()
            };
        }

        private void ValidateState()
        {
            CharacterAcquiredTraitAggregateState state = actor.Progression
                .CaptureAcquiredTraitState();
            CharacterAcquiredTraitInstanceState selected = null;
            if (traitInstanceId.Length > 0)
            {
                if (!state.TryGetInstance(traitInstanceId, out selected))
                    throw new InvalidOperationException("Causal trait checkpoint lost its instance.");
            }
            if (manifested != (selected != null)
                || erased != (selected?.erased == true)
                || reactionExecuted != (ReactionTriggerCount(state) == 1)
                || state.ActiveCount != (manifested && !erased ? 1 : 0))
            {
                throw new InvalidOperationException(
                    "Causal trait flags disagree with authoritative acquired-trait state.");
            }
            int physical = repository.GetEditorTestQuantity(sealStackId);
            int carried = carry.CountItem(MemoryErasureSealItemRules.ItemId);
            int expectedSeal = erased ? 0 : MemoryErasureSealItemRules.UseQuantity;
            if (physical != expectedSeal || carried != expectedSeal)
            {
                throw new InvalidOperationException(
                    "Causal trait physical seal and carry mirror disagree with terminal state.");
            }
        }

        private static int ReactionTriggerCount(
            CharacterAcquiredTraitAggregateState state)
        {
            if (state == null)
                return 0;
            return state.instances
                .Where(value => value != null)
                .SelectMany(value => value.reactionStates
                    ?? new List<CharacterAcquiredTraitReactionState>())
                .Where(value => value != null
                    && string.Equals(value.reactionId, ReactionId,
                        StringComparison.Ordinal))
                .Sum(value => value.totalTriggerCount);
        }

        private static CharacterCarryInventorySaveData CreateCarrySnapshot(
            string stackId,
            string operationId) => new()
        {
            items = new List<CharacterCarriedItemSaveData>
            {
                new()
                {
                    carriedStackId = stackId,
                    sourceStackId = stackId,
                    ownerOperationId = operationId,
                    itemId = MemoryErasureSealItemRules.ItemId,
                    quantity = MemoryErasureSealItemRules.UseQuantity,
                    components = new List<ItemInstanceComponentSaveData>()
                }
            }
        };

        private static JObject Serialize(object value, string label)
        {
            try
            {
                return JObject.Parse(JsonUtility.ToJson(
                    value ?? throw new ArgumentNullException(nameof(value))));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Failed to serialize causal acquired-trait " + label + ".",
                    exception);
            }
        }

        private JObject CaptureMemoryErasurePublications()
        {
            string json = new MemoryErasurePublicationSaveSection(memoryErasures)
                .Capture();
            MemoryErasurePublicationSaveSection.ValidateJson(json);
            return JObject.Parse(json);
        }

        private static T Deserialize<T>(JToken token, string label)
        {
            try
            {
                T value = JsonUtility.FromJson<T>(token?.ToString() ?? string.Empty);
                return value ?? throw new InvalidOperationException(label + " is null.");
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Failed to restore causal acquired-trait " + label + ".",
                    exception);
            }
        }

        private static void RequireFields(JObject value, string label, params string[] fields)
        {
            string[] actual = value.Properties().Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal).ToArray();
            string[] expected = fields.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    label + " fields mismatch. actual=" + string.Join(",", actual)
                    + "; expected=" + string.Join(",", expected));
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    [Serializable]
    private sealed class ProgressionEnvelope
    {
        public int level;
        public int currentExperience;
        public CharacterGrowthState growthState;
        public CharacterNarrativeLedger narrativeLedger;
        public CharacterAcquiredTraitAggregateState acquiredTraitState;
    }

    private sealed class AcquiredTraitDefinitionSource :
        IGameContentDefinitionSource
    {
        private readonly CharacterAcquiredTraitSettingsSO settings;
        private readonly CharacterAcquiredTraitModuleSO[] modules;

        internal AcquiredTraitDefinitionSource(
            CharacterAcquiredTraitSettingsSO settings,
            IEnumerable<CharacterAcquiredTraitModuleSO> modules)
        {
            this.settings = settings
                ?? throw new ArgumentNullException(nameof(settings));
            this.modules = (modules
                    ?? throw new ArgumentNullException(nameof(modules)))
                .Where(value => value != null)
                .ToArray();
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
        {
            if (typeof(T) == typeof(CharacterAcquiredTraitSettingsSO))
                return new[] { settings }.Cast<T>().ToArray();
            if (typeof(T) == typeof(CharacterAcquiredTraitModuleSO))
                return modules.Cast<T>().ToArray();
            return Array.Empty<T>();
        }

        public T RequireSingle<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> values = GetAll<T>();
            if (values.Count != 1)
            {
                throw new InvalidOperationException(
                    "Causal trait fixture expected one " + typeof(T).Name
                    + ", found " + values.Count + ".");
            }
            return values[0];
        }
    }

    private sealed class FixedCharacterWorld : ICharacterWorldQuery
    {
        private readonly CharacterActor[] actors;

        internal FixedCharacterWorld(params CharacterActor[] actors)
        {
            this.actors = (actors ?? Array.Empty<CharacterActor>())
                .Where(value => value != null)
                .ToArray();
        }

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => actors;
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal FixtureSet(IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures;

        internal IProductionCausalActionFixture Get(string actionId) => fixtures.Single(
            value => string.Equals(value.ActionId, actionId, StringComparison.Ordinal));

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private sealed class DeterministicIds : IPersistentIdGenerator
    {
        private readonly string scope;

        internal DeterministicIds(string scope) => this.scope = scope;

        public ItemInstanceId NewItemInstanceId() =>
            new("item-instance:causal-trait-memory:" + scope);
        public ItemStackId NewItemStackId() =>
            new("stack:causal-trait-memory:" + scope);
        public CharacterId NewCharacterId() =>
            new("character:causal-trait-memory:" + scope);
        public BuildingInstanceId NewBuildingInstanceId() =>
            new("building:causal-trait-memory:" + scope);
        public WildlifeHabitatPatchId NewWildlifeHabitatPatchId() =>
            new("wildlife-habitat:causal-trait-memory:" + scope);
    }

    private sealed class FixedClock : IGameClock, IGameCalendar
    {
        private int day;
        private int hour;

        internal FixedClock(int day, int hour)
        {
            this.day = day;
            this.hour = hour;
        }

        public float DeltaTime => 1f / 60f;
        public float Time => AbsoluteHour * 60f;
        public int FrameCount => 1;
        public bool IsPaused => false;
        public int Day => day;
        public int Hour => hour;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => hour / 24f * GameCalendarRules.SecondsPerDay;
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(day, hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(day, hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int nextDay, int nextHour)
        {
            day = Math.Max(1, nextDay);
            hour = Math.Clamp(nextHour, 0, 23);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
