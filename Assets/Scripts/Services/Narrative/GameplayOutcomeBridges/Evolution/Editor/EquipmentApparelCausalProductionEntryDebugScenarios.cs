#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class EquipmentApparelCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:equipment-apparel-acquisition";
    internal const string CraftActionId =
        "action:equipment-apparel-physical-crafted";
    internal const string EquipActionId =
        "action:equipment-apparel-equipped";

    private const string OwnerKind =
        "owner-kind:equipment-apparel-acquisition";
    private const string DomainStateSchema =
        "dungeonstory-causal-equipment-apparel-owner-state@3";
    private const string CharacterDisplayName = "연화";

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
                "An equipment-apparel checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(2, context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                CraftActionId,
                "equipment",
                EvolutionOutcomeIds.ApparelPhysicalCompleted,
                context.CaptureState,
                context.Craft,
                context.RequireCraftOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null,
                captureNarrativeEvidence: context.CaptureNarrativeEvidence),
            new DelegateProductionCausalActionFixture(
                EquipActionId,
                "equipment",
                EvolutionOutcomeIds.ApparelChanged,
                () => new Dictionary<string, object>(),
                context.Equip,
                context.RequireEquipOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null,
                captureNarrativeEvidence: context.CaptureNarrativeEvidence)
        };
    }

    public static bool RunAll(bool log = false)
    {
        const string OwnerIdValue = "owner:causal-equipment-apparel:focused";
        JObject craftedCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-equipment-apparel:craft",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture craft = fixtures.Get(CraftActionId);
            IReadOnlyDictionary<string, object> before = craft.CaptureState();
            Require((int)before["equipment.materialQuantity"] == 4
                    && (int)before["equipment.outputQuantity"] == 0
                    && !(bool)before["equipment.crafted"],
                "The apparel chain did not start with exact material authority.");
            Require(craft.Execute(),
                "The real apparel physical transaction did not execute.");
            Require(craft.RequireExactOutcome().HasCanonicalPayloadHash,
                "The apparel physical completion has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = craft.CaptureState();
            Require((int)after["equipment.materialQuantity"] == 2
                    && (int)after["equipment.outputQuantity"] == 1
                    && (bool)after["equipment.crafted"]
                    && !(bool)after["equipment.equipped"],
                "Apparel crafting did not consume two materials and create one unique item.");
            craftedCheckpoint = ((IProductionCausalOwnerCheckpointFixture)craft)
                .CaptureOwnerCheckpoint();
        }

        JObject equippedCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-equipment-apparel:equip",
                   OwnerIdValue,
                   craftedCheckpoint)))
        {
            IProductionCausalActionFixture equip = fixtures.Get(EquipActionId);
            IReadOnlyDictionary<string, object> before = fixtures
                .Get(CraftActionId).CaptureState();
            Require((bool)before["equipment.crafted"]
                    && !(bool)before["equipment.equipped"]
                    && (int)before["equipment.outputQuantity"] == 1,
                "Fresh restore lost the crafted apparel before equipping.");
            Require(equip.Execute(),
                "The real character apparel aggregate did not equip the crafted item.");
            Require(equip.RequireExactOutcome().HasCanonicalPayloadHash,
                "The apparel change has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = fixtures
                .Get(CraftActionId).CaptureState();
            Require((bool)after["equipment.equipped"]
                    && (int)after["equipment.equippedCount"] == 1
                    && string.Equals(
                        (string)after["equipment.outputState"],
                        WorldItemStackState.Carried.ToString(),
                        StringComparison.Ordinal),
                "Equipping did not preserve the unique physical apparel join.");
            equippedCheckpoint = ((IProductionCausalOwnerCheckpointFixture)equip)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-equipment-apparel:final-restore",
                   OwnerIdValue,
                   equippedCheckpoint)))
        {
            IReadOnlyDictionary<string, object> restored = fixtures
                .Get(CraftActionId).CaptureState();
            Require((bool)restored["equipment.crafted"]
                    && (bool)restored["equipment.equipped"]
                    && (int)restored["equipment.equippedCount"] == 1,
                "The crafted and equipped apparel did not survive fresh restore.");
            Require(!fixtures.Get(CraftActionId).Execute()
                    && !fixtures.Get(EquipActionId).Execute(),
                "A terminal apparel acquisition step executed twice after restore.");
        }

        JObject tampered = (JObject)equippedCheckpoint.DeepClone();
        ((JObject)tampered["domainState"])["outputInstanceId"] =
            "item-instance:tampered";
        bool tamperRejected = false;
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-equipment-apparel:tampered",
                OwnerIdValue,
                tampered));
        }
        catch (InvalidOperationException)
        {
            tamperRejected = true;
        }
        Require(tamperRejected,
            "A tampered apparel instance identity was accepted.");

        if (log)
            Debug.Log("Equipment apparel craft -> equip causal owner family PASS");
        return true;
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly string characterId;
        private readonly string orderId;
        private readonly ApparelPhysicalTransactionDebugScenarios.Fixture physical;
        private readonly CharacterAiWorldRegistry world;
        private readonly GameObject actorObject;
        private readonly CharacterSO actorData;
        private readonly CharacterActor actor;
        private readonly CharacterApparelAggregate apparel;
        private readonly IDisposable apparelEventSubscription;
        private ApparelWorkOrderSaveData order;
        private string sourceStackId = string.Empty;
        private string outputStackId = string.Empty;
        private string outputInstanceId = string.Empty;
        private bool crafted;
        private bool equipped;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string stableSeed = HasPersistentOwner ? this.ownerId : causalRunId;
            string scope = ProductionCausalOutcomeEvidence.Sha256Text(stableSeed)
                .Substring("sha256:".Length, 24);
            characterId = "character:causal-equipment-apparel:" + scope;
            orderId = "apparel-order:causal:" + scope;
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(this.ownerId, OwnerKind)
                : causalRunId + ":equipment-apparel-ledger";
            physical = new ApparelPhysicalTransactionDebugScenarios.Fixture(
                "causal-" + scope,
                runId: ledgerRunId,
                persistentIdGenerator: new DeterministicIds(scope));

            IGameContentDefinitionSource content =
                CharacterAiEditorTestDependencies.ContentDefinitions;
            var apparelCatalog = new ResourceApparelDefinitionCatalog(content);
            var textileCatalog = new ResourceTextileMaterialCatalog(content);
            if (!apparelCatalog.TryGet(
                    ApparelPhysicalTransactionDebugScenarios.ApparelDefinitionId,
                    out ApparelDefinitionSO definition)
                || !textileCatalog.TryGet(
                    ApparelPhysicalTransactionDebugScenarios.MaterialDefinitionId,
                    out TextileMaterialDefinitionSO textile)
                || !string.Equals(
                    definition.PhysicalItemId,
                    ApparelPhysicalTransactionDebugScenarios.OutputItemId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    textile.PhysicalItemId,
                    ApparelPhysicalTransactionDebugScenarios.MaterialItemId,
                    StringComparison.Ordinal))
            {
                physical.Dispose();
                throw new InvalidOperationException(
                    "Authored hauling-harness apparel or common-wool material is missing.");
            }

            world = new CharacterAiWorldRegistry(
                new SceneRuntimeRegistry<CharacterActor>(),
                new SceneRuntimeRegistry<WildlifeActor>(),
                new SceneRuntimeRegistry<BuildableObject>(),
                new SceneRuntimeRegistry<IWarehouseFacility>(),
                new SceneRuntimeRegistry<IRetailFacility>(),
                Proxy<IGameSessionStateProvider>(),
                Proxy<IRestoreWorldCandidateQuery>(),
                Proxy<ICharacterLifePublicationService>());
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                CharacterDisplayName,
                "human");
            actorObject = new GameObject("EquipmentApparelCausal_" + scope);
            actorObject.AddComponent<SpriteRenderer>();
            actor = actorObject.AddComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.Identity.SetPersistentId(characterId);
            actor.Initialization(actorData);
            world.RegisterCharacter(actor);
            if (!CharacterPersistentIdentity.Require(actor).Equals(
                    new CharacterId(characterId)))
            {
                Dispose();
                throw new InvalidOperationException(
                    "The apparel wearer did not retain its deterministic character identity.");
            }

            var anatomy = new AnatomyAttachmentQuery(
                world,
                CharacterAiEditorTestDependencies.BodyHealthRuntime);
            var availability = new ApparelAvailabilityIndex(
                physical.WorldItems,
                apparelCatalog,
                textileCatalog);
            var identityBus = new GameEventBus();
            apparelEventSubscription = identityBus.Subscribe<ApparelChangedEvent>(_ => { });
            apparel = new CharacterApparelAggregate(
                new CharacterApparelAggregateStateStore(
                    new DungeonRuntimeAggregateRootStore()),
                apparelCatalog,
                anatomy,
                physical.WorldItems,
                physical.WorldItems,
                world,
                availability,
                new CharacterIdentityEventPublisher(identityBus),
                physical.OutcomeFixture.Calendar,
                physical.OutcomeFixture.ApparelChanges);

            try
            {
                if (checkpoint == null)
                {
                    sourceStackId = physical.AddMaterialSource(4);
                    order = physical.CreateOrder(sourceStackId, 2, orderId);
                }
                else
                {
                    RestoreOwnerCheckpoint(checkpoint);
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
            ? OwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            WorldItemStackSnapshot output = FindOutput();
            float durability = 0f;
            if (output != null
                && ApparelItemStateCodec.TryRead(
                    output.Components,
                    out ApparelInstanceState state))
            {
                durability = state.durability;
            }
            return new Dictionary<string, object>
            {
                ["equipment.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-equipment-apparel",
                ["equipment.characterId"] = characterId,
                ["equipment.facilityId"] = "building:qa:apparel-bench",
                ["equipment.apparelDefinitionId"] =
                    ApparelPhysicalTransactionDebugScenarios.ApparelDefinitionId,
                ["equipment.materialQuantity"] = sourceStackId.Length == 0
                    ? 0
                    : physical.Repository.GetEditorTestQuantity(sourceStackId),
                ["equipment.outputQuantity"] = output?.Quantity ?? 0,
                ["equipment.outputStackId"] = outputStackId,
                ["equipment.outputInstanceId"] = outputInstanceId,
                ["equipment.outputState"] = output?.State.ToString() ?? "Missing",
                ["equipment.apparelDurability"] = durability,
                ["equipment.apparelVersion"] = apparel.Version,
                ["equipment.equippedCount"] = apparel
                    .GetEquipped(new CharacterId(characterId)).Count,
                ["equipment.crafted"] = crafted,
                ["equipment.equipped"] = equipped
            };
        }

        internal bool Craft()
        {
            ThrowIfDisposed();
            if (crafted || equipped || order == null)
                return false;
            ApparelPhysicalTransactionResult result = physical.Transaction
                .ExecuteCraftOrResume(
                    order,
                    physical.Facility,
                    ApparelPhysicalTransactionDebugScenarios.OutputItemId,
                    ApparelPhysicalTransactionDebugScenarios.CreateApparelComponent(),
                    markForSale: false,
                    characterId,
                    CharacterDisplayName,
                    CraftsmanshipQualityTier.Normal,
                    rejectedBelowMinimum: false);
            if (!result.IsCompleted)
                return false;
            outputStackId = result.OutputStackId;
            outputInstanceId = result.OutputInstanceId;
            crafted = RequireCraftOutcome(false).HasCanonicalPayloadHash;
            ValidateState();
            return crafted;
        }

        internal bool Equip()
        {
            ThrowIfDisposed();
            if (!crafted || equipped || outputInstanceId.Length == 0)
                return false;
            var itemInstanceId = new ItemInstanceId(outputInstanceId);
            if (!apparel.TryPlanChange(
                    new CharacterId(characterId),
                    itemInstanceId,
                    out ApparelChangePlan plan,
                    out _)
                || !apparel.TryCommitChange(plan, out _))
            {
                return false;
            }
            equipped = apparel.GetEquipped(new CharacterId(characterId))
                    .Any(value => value.ItemInstanceId.Equals(itemInstanceId))
                && RequireEquipOutcome(false).HasCanonicalPayloadHash;
            ValidateState();
            return equipped;
        }

        internal GameplayOutcomeReplayIdentity RequireCraftOutcome() =>
            RequireCraftOutcome(true);

        internal GameplayOutcomeReplayIdentity RequireEquipOutcome() =>
            RequireEquipOutcome(true);

        internal JObject CaptureOutcomeEvidence(GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(
                physical.OutcomeFixture.Ledger,
                identity);

        internal IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot>
            CaptureNarrativeEvidence(GameplayEntityId subject, int maximumCount) =>
            ProductionCausalNarrativeEvidence.Capture(
                physical.OutcomeFixture.Ledger, subject, maximumCount);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "This equipment-apparel context has no persistent owner.");
            }
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                Serialize(CaptureDomainState(), "equipment-apparel state"),
                physical.OutcomeFixture.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            apparelEventSubscription?.Dispose();
            if (actor != null)
                world?.UnregisterCharacter(actor);
            if (actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
            if (actorData != null)
                UnityEngine.Object.DestroyImmediate(actorData);
            physical?.Dispose();
        }

        private GameplayOutcomeReplayIdentity RequireCraftOutcome(bool throwOnFailure)
        {
            string operationId = "apparel-craft-output-batch:"
                + orderId + ":0000";
            var key = new GameplayResultKey(
                EvolutionOutcomeIds.ApparelPhysicalProducerId,
                new GameplayOperationId(operationId),
                0L,
                0);
            return RequireExact(
                key,
                EvolutionOutcomeIds.ApparelPhysicalCompleted,
                throwOnFailure);
        }

        private GameplayOutcomeReplayIdentity RequireEquipOutcome(bool throwOnFailure)
        {
            long revision = physical.OutcomeFixture.ApparelChanges
                .Capture().transitionRevision;
            string operationId = "apparel-change:"
                + characterId + ":revision:" + revision.ToString("D8");
            var key = new GameplayResultKey(
                EvolutionOutcomeIds.ApparelProducerId,
                new GameplayOperationId(operationId),
                revision,
                0);
            return RequireExact(
                key,
                EvolutionOutcomeIds.ApparelChanged,
                throwOnFailure);
        }

        private GameplayOutcomeReplayIdentity RequireExact(
            GameplayResultKey key,
            GameplayOutcomeTypeId expectedType,
            bool throwOnFailure)
        {
            GameplayOutcomeLedger ledger = physical.OutcomeFixture.Ledger;
            bool valid = ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot exact)
                && exact != null
                && string.Equals(
                    exact.outcomeTypeId,
                    expectedType.Value,
                    StringComparison.Ordinal);
            if (!valid && throwOnFailure)
            {
                throw new InvalidOperationException(
                    "Causal equipment-apparel production entry did not publish exact outcome "
                    + expectedType.Value + ".");
            }
            return valid ? identity : default;
        }

        private DomainStateSaveData CaptureDomainState() => new()
        {
            schemaVersion = 3,
            schema = DomainStateSchema,
            characterId = characterId,
            orderId = orderId,
            sourceStackId = sourceStackId,
            outputStackId = outputStackId,
            outputInstanceId = outputInstanceId,
            physicalItems = physical.WorldItems.Capture(),
            order = order,
            apparel = apparel.CaptureApparel().ToList(),
            apparelPolicies = apparel.CaptureApparelPolicies().ToList(),
            apparelChanges = physical.OutcomeFixture.ApparelChanges.Capture(),
            apparelPhysical = physical.OutcomeFixture.ApparelPhysicalPublications
                .Capture(),
            crafted = crafted,
            equipped = equipped
        };

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                physical.OutcomeFixture.Ledger);
            if (state["apparelChanges"] is not JObject apparelChangesJson)
            {
                throw new InvalidOperationException(
                    "Causal equipment-apparel checkpoint is missing its apparel observation journal.");
            }
            ApparelChangePublicationSaveSection.ValidateJson(
                apparelChangesJson.ToString(Formatting.None));
            if (state["apparelPhysical"] is not JObject apparelPhysicalJson)
            {
                throw new InvalidOperationException(
                    "Causal equipment-apparel checkpoint is missing its physical observation journal.");
            }
            ApparelPhysicalPublicationSaveSection.ValidateJson(
                apparelPhysicalJson.ToString(Formatting.None));
            DomainStateSaveData saved = Deserialize<DomainStateSaveData>(
                state,
                "equipment-apparel state");
            if (saved.schemaVersion != 3
                || !string.Equals(saved.schema, DomainStateSchema, StringComparison.Ordinal)
                || !string.Equals(saved.characterId, characterId, StringComparison.Ordinal)
                || !string.Equals(saved.orderId, orderId, StringComparison.Ordinal)
                || saved.physicalItems == null
                || saved.order == null
                || saved.apparelChanges == null
                || saved.apparelPhysical == null
                || saved.apparelChanges.transitionRevision
                    != (saved.equipped ? 1L : 0L)
                || saved.apparelPhysical.observations.Count
                    != (saved.crafted ? 1 : 0))
            {
                throw new InvalidOperationException(
                    "Causal equipment-apparel owner identity or state header changed.");
            }
            physical.WorldItems.Restore(saved.physicalItems);
            order = saved.order;
            sourceStackId = saved.sourceStackId ?? string.Empty;
            outputStackId = saved.outputStackId ?? string.Empty;
            outputInstanceId = saved.outputInstanceId ?? string.Empty;
            crafted = saved.crafted;
            equipped = saved.equipped;
            ApparelChangePublicationSaveData restoredChanges =
                ApparelChangePublicationRuntime.ValidateAndClone(
                    saved.apparelChanges);
            GameplayOutcomeLedgerSaveData restoredLedger =
                physical.OutcomeFixture.Ledger.CaptureGameplayOutcomes();
            ApparelLoadoutChangedFactPreflight.ValidateJournal(
                restoredChanges,
                restoredLedger);
            ApparelPhysicalPublicationSaveData restoredPhysical =
                ApparelPhysicalPublicationRuntime.ValidateAndClone(
                    saved.apparelPhysical);
            ApparelPhysicalCompletedFactPreflight.ValidateJournal(
                restoredPhysical,
                restoredLedger);
            ApparelPhysicalCompletedFactPreflight.ValidateOwners(restoredPhysical, new[] { saved.order });
            physical.OutcomeFixture.ApparelChanges.Restore(restoredChanges);
            physical.OutcomeFixture.ApparelPhysicalPublications.Restore(
                restoredPhysical);
            var report = new DungeonGameRestoreReport();
            CharacterApparelRestoreCandidate candidate = apparel.PrepareRestoreApparel(
                saved.apparel,
                saved.apparelPolicies,
                report);
            if (!report.Success)
            {
                throw new InvalidOperationException(
                    "Causal equipment-apparel restore was rejected: "
                    + string.Join(" | ", report.Errors));
            }
            apparel.PublishRestoreApparel(candidate);
        }

        private void ValidateState()
        {
            if (order == null
                || !string.Equals(order.orderId, orderId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(sourceStackId))
            {
                throw new InvalidOperationException(
                    "Causal equipment-apparel order identity changed.");
            }
            WorldItemStackSnapshot output = FindOutput();
            int equippedCount = apparel.GetEquipped(new CharacterId(characterId)).Count;
            bool physicalJoin = output != null
                && output.Quantity == 1
                && string.Equals(output.StackId, outputStackId, StringComparison.Ordinal)
                && string.Equals(
                    output.ItemInstanceId,
                    outputInstanceId,
                    StringComparison.Ordinal)
                && ApparelItemStateCodec.TryRead(
                    output.Components,
                    out ApparelInstanceState itemState)
                && string.Equals(
                    itemState.apparelDefinitionId,
                    ApparelPhysicalTransactionDebugScenarios.ApparelDefinitionId,
                    StringComparison.Ordinal);
            if (crafted != physicalJoin
                || equipped != (equippedCount == 1)
                || !crafted && (outputStackId.Length > 0 || outputInstanceId.Length > 0)
                || equipped && (!crafted
                    || output.State != WorldItemStackState.Carried
                    || !string.Equals(
                        output.DestinationId,
                        CharacterApparelAggregate.EquippedDestinationPrefix + characterId,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Causal equipment-apparel flags disagree with physical or equipped authority.");
            }
        }

        private WorldItemStackSnapshot FindOutput()
        {
            if (outputStackId.Length == 0)
                return null;
            return physical.WorldItems.GetAllStacks().SingleOrDefault(value =>
                value != null
                && string.Equals(value.StackId, outputStackId, StringComparison.Ordinal));
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    [Serializable]
    private sealed class DomainStateSaveData
    {
        public int schemaVersion;
        public string schema = string.Empty;
        public string characterId = string.Empty;
        public string orderId = string.Empty;
        public string sourceStackId = string.Empty;
        public string outputStackId = string.Empty;
        public string outputInstanceId = string.Empty;
        public DungeonPhysicalItemSaveData physicalItems;
        public ApparelWorkOrderSaveData order;
        public List<EquippedApparelSaveData> apparel = new();
        public List<CharacterApparelPolicySaveData> apparelPolicies = new();
        public ApparelChangePublicationSaveData apparelChanges;
        public ApparelPhysicalPublicationSaveData apparelPhysical;
        public bool crafted;
        public bool equipped;
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
        private int itemInstanceSequence;
        private int itemStackSequence;
        private int characterSequence;
        private int buildingSequence;
        private int wildlifeSequence;

        internal DeterministicIds(string scope) => this.scope = scope;

        public ItemInstanceId NewItemInstanceId() =>
            new("item-instance:causal-equipment-apparel:" + scope + ":"
                + (++itemInstanceSequence).ToString("D4"));
        public ItemStackId NewItemStackId() =>
            new("stack:causal-equipment-apparel:" + scope + ":"
                + (++itemStackSequence).ToString("D4"));
        public CharacterId NewCharacterId() =>
            new("character:causal-equipment-apparel:" + scope + ":"
                + (++characterSequence).ToString("D4"));
        public BuildingInstanceId NewBuildingInstanceId() =>
            new("building:causal-equipment-apparel:" + scope + ":"
                + (++buildingSequence).ToString("D4"));
        public WildlifeHabitatPatchId NewWildlifeHabitatPatchId() =>
            new("wildlife-habitat:causal-equipment-apparel:" + scope + ":"
                + (++wildlifeSequence).ToString("D4"));
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
        {
            throw new InvalidOperationException(
                "Causal " + label + " checkpoint must be an object.");
        }
        T result = JsonUtility.FromJson<T>(json.ToString(Formatting.None));
        return result ?? throw new InvalidOperationException(
            "Causal " + label + " checkpoint deserialized to null.");
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
