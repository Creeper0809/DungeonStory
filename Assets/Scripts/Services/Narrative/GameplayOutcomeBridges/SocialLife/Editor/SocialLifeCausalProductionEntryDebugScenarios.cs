#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class SocialLifeCausalProductionEntryDebugScenarios
{
    internal const string CausalSocialOwnerRestoreKind =
        "owner-family:social-conflict-apology";
    internal const string AssaultConflictActionId =
        "action:social-assault-conflict";
    internal const string ApologyActionId =
        "action:social-apology";

    private const string CausalSocialOwnerKind =
        "owner-kind:social-conflict-apology";
    private const string CausalSocialDomainStateSchema =
        "dungeonstory-causal-social-owner-state@3";
    private const int AbsoluteDay = 23;

    internal static IReadOnlyList<IProductionCausalActionFixture>
        CreateCausalSocialFixtures(
            string causalRunId,
            string ownerId = null,
            JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException(
                "A causal run ID is required.",
                nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A social owner checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new CausalSocialFixtureContext(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                AssaultConflictActionId,
                "relationship",
                SocialLifeOutcomeIds.ConflictResolved,
                context.CaptureState,
                context.Assault,
                context.RequireConflictOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.OwnerId,
                context.OwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalSocialOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                ApologyActionId,
                "relationship",
                SocialLifeOutcomeIds.ApologyResolved,
                () => new Dictionary<string, object>(),
                context.Apologize,
                context.RequireApologyOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.OwnerId,
                context.OwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalSocialOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    public static void VerifyObserverCheckpoint()
    {
        JObject checkpoint;
        using (var source = new CausalSocialFixtureContext("run:b92-causal", "owner:b92-causal", null))
        {
            if (!source.Assault() || !source.Apologize())
                throw new InvalidOperationException("B92 causal combat/apology entry failed.");
            checkpoint = JObject.Parse(source.CaptureOwnerCheckpoint().ToString());
        }
        using var restored = new CausalSocialFixtureContext("run:b92-restored", "owner:b92-causal", checkpoint);
        restored.RequireConflictOutcome(); restored.RequireApologyOutcome();
        if (restored.Assault() || restored.Apologize())
            throw new InvalidOperationException("B92 restored causal owner repeated an effect.");
        Debug.Log("B92_SOCIAL_CAUSAL_RESTORE=PASS");
    }

    private sealed class CausalSocialFixtureContext : IDisposable
    {
        private readonly string ownerId;
        private readonly string offenderId;
        private readonly string recipientId;
        private readonly string assaultOperationId;
        private readonly string apologyOperationId;
        private readonly FixedGameClock clock;
        private readonly FixedCalendar calendar;
        private readonly GameEventBus events;
        private readonly CharacterVitalsActivityObserver vitalsActivities;
        private readonly GameplayOutcomeLedger ledger;
        private readonly MigratedProducerOutcomeRuntime migrated;
        private readonly CombatActionPublicationRuntime combatActions;
        private readonly CharacterIdentityStateStore identityStates;
        private readonly CharacterRelationshipMemoryService memories;
        private readonly CharacterBodyHealthRuntime bodyHealth;
        private readonly DomainFactDispatcher facts;
        private readonly ActorFixture offender;
        private readonly ActorFixture recipient;
        private readonly SocialIdentityEventAdapter socialAdapter;
        private readonly SocialInteractionPublicationRuntime socialOutcomes;
        private readonly IDisposable conflictObservation, apologyObservation;
        private readonly CombatCommandResultApplier combatResults;
        private bool assaultExecuted;
        private bool apologyExecuted;
        private bool disposed;

        internal CausalSocialFixtureContext(
            string causalRunId,
            string ownerId,
            JObject ownerCheckpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string stableSeed = HasPersistentOwner ? this.ownerId : causalRunId;
            string stableScope = ProductionCausalOutcomeEvidence
                .Sha256Text(stableSeed)
                .Substring("sha256:".Length, 24);
            offenderId = "character:causal-social-offender:" + stableScope;
            recipientId = "character:causal-social-recipient:" + stableScope;
            assaultOperationId = "combat:social-assault:" + stableScope;
            apologyOperationId = "social:apology:" + stableScope;

            clock = new FixedGameClock(AbsoluteDay);
            calendar = new FixedCalendar(AbsoluteDay);
            events = new GameEventBus();
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    CausalSocialOwnerKind)
                : causalRunId + ":social-ledger";
            IKoreanJosaFormatter josa = new KoreanJosaFormatter();
            var migratedAdapter = new MigratedProducerOutcomeAdapter();
            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new CombatDamageOutcomeDescriptor(),
                    new SocialConflictOutcomeDescriptor(josa),
                    new ApologyOutcomeDescriptor(josa)
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CombatDamageOutcomeAdapter(),
                    new SocialConflictOutcomeAdapter(),
                    new ApologyOutcomeAdapter(),
                    migratedAdapter
                },
                new IGameplayOutcomeDescriptorCatalog[]
                {
                    new MigratedProducerOutcomeDescriptorCatalog(josa)
                });
            ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 16,
                    largePageCount: 4,
                    knownResultKeyCapacity: 128),
                new GameplayOutcomeRunId(ledgerRunId),
                1L);
            GameplayOutcomeRecorder recorder = new(ledger, registry, events);
            var roots = new DungeonRuntimeAggregateRootStore();
            migrated = new MigratedProducerOutcomeRuntime(
                roots,
                ledger,
                recorder,
                ledger);
            facts = new DomainFactDispatcher();
            var group = new GameplayDomainFactCommitGroup(recorder, ledger);
            conflictObservation = SocialInteractionFactMapping.CreateConflictBinding(registry, ledger, recorder, group).Connect(facts);
            apologyObservation = SocialInteractionFactMapping.CreateApologyBinding(registry, ledger, recorder, group).Connect(facts);
            socialOutcomes = new SocialInteractionPublicationRuntime(facts, facts);
            facts.Subscribe(
                "log:combat.action",
                new CombatActionFactObserver(
                    ledger,
                    recorder,
                    migrated,
                    new GameplayDomainFactCommitGroup(recorder, ledger)));
            combatActions = new CombatActionPublicationRuntime(
                new DungeonStory.Foundation.UnityUiClock(),
                roots,
                facts,
                facts);

            bodyHealth = new CharacterBodyHealthRuntime(new DungeonStory.Foundation.UnityRealtimeClock(),
                CharacterAiEditorTestDependencies.WorldRegistry,
                clock,
                events,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                new ResourceAnatomyProfileCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())),
                roots,
                CharacterAiEditorTestDependencies.GameCalendar);
            vitalsActivities = new CharacterVitalsActivityObserver(
                events,
                CharacterAiEditorTestDependencies.WorldRegistry);
            vitalsActivities.Initialize();
            offender = new ActorFixture(
                offenderId,
                "서린",
                4301,
                bodyHealth,
                events);
            recipient = new ActorFixture(
                recipientId,
                "다온",
                4302,
                bodyHealth,
                events,
                218);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                offender.Actor);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                recipient.Actor);

            var world = new FixedCharacterWorld(
                new[] { offender.Actor, recipient.Actor });
            identityStates = new CharacterIdentityStateStore();
            var router = new CharacterIdentityRuleRouter();
            memories = new CharacterRelationshipMemoryService(
                router,
                identityStates);
            var moods = new CharacterMoodPolicyService(
                router,
                new CharacterPersistentNeedRuntime(identityStates, calendar));
            socialAdapter = new SocialIdentityEventAdapter(
                events,
                world,
                moods,
                memories,
                identityStates,
                socialOutcomes,
                EmptyNarrativeQuery.Instance,
                EmptyBuildingWorld.Instance,
                new RoomFacilityPolicyService(new RoomLayoutCache()));
            socialAdapter.Start();

            combatResults = new CombatCommandResultApplier(
                CharacterAiEditorTestDependencies.CombatEquipment,
                bodyHealth,
                bodyHealth,
                bodyHealth,
                combatActions,
                new NoCoverDurabilityRegistry(),
                clock,
                new NoWorldUiHierarchy(),
                events,
                world,
                EmptyBuildingWorld.Instance,
                new RoomFacilityPolicyService(new RoomLayoutCache()),
                calendar);

            try
            {
                if (ownerCheckpoint != null)
                    RestoreOwnerCheckpoint(ownerCheckpoint);
                else
                    RequireInitialState();
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
            ? CausalSocialOwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            return new Dictionary<string, object>
            {
                ["social.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-social",
                ["social.offenderId"] = offenderId,
                ["social.recipientId"] = recipientId,
                ["social.recipientHealth"] = bodyHealth
                    .GetVitals(recipient.Actor).CurrentHealth,
                ["social.recipientMood"] = recipient.Actor.Mood.Value,
                ["social.relationshipSentiment"] = recipient.Actor
                    .SocialMemory.GetRelationshipSentiment(offender.Actor),
                ["social.canForgive"] = memories.CanForgive(
                    recipient.Actor,
                    offender.Actor,
                    restitutionProvided: true),
                ["social.identityRuleStateCount"] = identityStates.Capture()
                    .Sum(value => value?.rules?.Count ?? 0),
                ["social.assaultExecuted"] = assaultExecuted,
                ["social.apologyExecuted"] = apologyExecuted
            };
        }

        internal bool Assault()
        {
            ThrowIfDisposed();
            if (assaultExecuted
                || apologyExecuted
                || memories.CanForgive(
                    recipient.Actor,
                    offender.Actor,
                    restitutionProvided: true))
            {
                return false;
            }

            float healthBefore = bodyHealth.GetVitals(
                recipient.Actor).CurrentHealth;
            var target = new CombatParticipantRef(recipient.Actor);
            if (!combatResults.TryReserveDamageOutcome(
                    target,
                    assaultOperationId,
                    1L,
                    out ReservedCombatAction reserved,
                    out _,
                    out _))
            {
                return false;
            }

            var result = new CombatAttackResult(
                executed: true,
                hit: true,
                coverBlocked: false,
                evaded: false,
                bodyPart: CombatBodyPart.Torso,
                rawDamage: 20f,
                appliedDamage: 20f,
                bleeding: 0f,
                suppression: 1f,
                armorDurabilityDamage: 0f,
                armorInstanceId: string.Empty,
                failureReason: string.Empty,
                nonlethal: true);
            CombatOutcomeApplyResult applied = combatResults.Apply(
                target,
                result,
                offender.Actor,
                offender.Actor.Identity.DisplayName,
                CombatDamageType.Blunt,
                assaultOperationId,
                1L,
                reserved,
                new CombatOutcomeMechanicalMutation(
                    apply: () => string.Empty,
                    rollback: () => { },
                    complete: () => { }));
            assaultExecuted = applied.Succeeded
                && bodyHealth.GetVitals(recipient.Actor).CurrentHealth
                    < healthBefore
                && memories.CanForgive(
                    recipient.Actor,
                    offender.Actor,
                    restitutionProvided: true)
                && RequireExactOutcome(
                    SocialLifeOutcomeIds.ConflictProducerId,
                    assaultOperationId,
                    SocialLifeOutcomeIds.ConflictResolved,
                    throwOnFailure: false).HasCanonicalPayloadHash;
            return assaultExecuted;
        }

        internal bool Apologize()
        {
            ThrowIfDisposed();
            if (!assaultExecuted
                || apologyExecuted
                || !memories.CanForgive(
                    recipient.Actor,
                    offender.Actor,
                    restitutionProvided: true))
            {
                return false;
            }

            var command = new CharacterApologyCommandRuntime(
                memories,
                events,
                calendar,
                new SocialOffenseHistory(ledger),
                () => apologyOperationId);
            if (!command.TryApologize(
                    offender.Actor,
                    recipient.Actor,
                    restitutionProvided: true,
                    out _))
            {
                return false;
            }
            apologyExecuted = !memories.CanForgive(
                    recipient.Actor,
                    offender.Actor,
                    restitutionProvided: true)
                && RequireExactOutcome(
                    SocialLifeOutcomeIds.ApologyProducerId,
                    apologyOperationId,
                    SocialLifeOutcomeIds.ApologyResolved,
                    throwOnFailure: false).HasCanonicalPayloadHash;
            return apologyExecuted;
        }

        internal GameplayOutcomeReplayIdentity RequireConflictOutcome() =>
            RequireExactOutcome(
                SocialLifeOutcomeIds.ConflictProducerId,
                assaultOperationId,
                SocialLifeOutcomeIds.ConflictResolved,
                throwOnFailure: true);

        internal GameplayOutcomeReplayIdentity RequireApologyOutcome() =>
            RequireExactOutcome(
                SocialLifeOutcomeIds.ApologyProducerId,
                apologyOperationId,
                SocialLifeOutcomeIds.ApologyResolved,
                throwOnFailure: true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "This causal social context has no persistent owner.");
            }
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                CausalSocialOwnerKind,
                CausalSocialOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 3,
                    ["schema"] = CausalSocialDomainStateSchema,
                    ["offenderId"] = offenderId,
                    ["recipientId"] = recipientId,
                    ["assaultOperationId"] = assaultOperationId,
                    ["apologyOperationId"] = apologyOperationId,
                    ["identityStates"] = Serialize(
                        new IdentityStateEnvelope
                        {
                            states = identityStates.Capture()
                                .Select(value => value?.Clone())
                                .Where(value => value != null)
                                .ToList()
                        },
                        "identity state"),
                    ["offenderStats"] = CaptureStats(offender.Actor),
                    ["recipientStats"] = CaptureStats(recipient.Actor),
                    ["offenderSocialMemory"] = Serialize(
                        offender.Actor.SocialMemory.CaptureSnapshot(),
                        "offender social memory"),
                    ["recipientSocialMemory"] = Serialize(
                        recipient.Actor.SocialMemory.CaptureSnapshot(),
                        "recipient social memory"),
                    ["bodyHealth"] = Serialize(
                        bodyHealth.Capture(),
                        "body-health state"),
                    ["outcomeOwners"] = Serialize(
                        migrated.Capture(),
                        "outcome owners"),
                    ["socialObservations"] = Serialize(socialOutcomes.Capture(), "social observations"),
                    ["combatActionPending"] = Serialize(
                        combatActions.Capture(),
                        "combat action pending"),
                    ["assaultExecuted"] = assaultExecuted,
                    ["apologyExecuted"] = apologyExecuted
                },
                ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            vitalsActivities?.Dispose();
            socialAdapter?.Dispose();
            conflictObservation?.Dispose(); apologyObservation?.Dispose();
            if (offender?.Actor != null)
            {
                CharacterAiEditorTestDependencies.WorldRegistry
                    .UnregisterCharacter(offender.Actor);
            }
            if (recipient?.Actor != null)
            {
                CharacterAiEditorTestDependencies.WorldRegistry
                    .UnregisterCharacter(recipient.Actor);
            }
            facts?.Dispose();
            recipient?.Dispose();
            offender?.Dispose();
        }

        private void RequireInitialState()
        {
            if (memories.CanForgive(
                    recipient.Actor,
                    offender.Actor,
                    restitutionProvided: true)
                || identityStates.Capture().Count != 0
                || bodyHealth.GetVitals(recipient.Actor).CurrentHealth <= 0f)
            {
                throw new InvalidOperationException(
                    "Causal social fixture did not begin from a clean live relationship.");
            }
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                CausalSocialOwnerKind,
                CausalSocialOwnerRestoreKind,
                ledger);
            RequireFields(
                state,
                "causal social owner state",
                "schemaVersion",
                "schema",
                "offenderId",
                "recipientId",
                "assaultOperationId",
                "apologyOperationId",
                "identityStates",
                "offenderStats",
                "recipientStats",
                "offenderSocialMemory",
                "recipientSocialMemory",
                "bodyHealth",
                "outcomeOwners",
                "combatActionPending",
                "socialObservations",
                "assaultExecuted",
                "apologyExecuted");
            if ((int)state["schemaVersion"] != 3
                || !string.Equals(
                    (string)state["schema"],
                    CausalSocialDomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["offenderId"],
                    offenderId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["recipientId"],
                    recipientId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["assaultOperationId"],
                    assaultOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["apologyOperationId"],
                    apologyOperationId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Causal social owner identity or operation IDs changed.");
            }

            IdentityStateEnvelope identity = Deserialize<IdentityStateEnvelope>(
                state["identityStates"],
                "identity state");
            CharacterTraitSO[] traitCatalog = offender.Actor.Progression
                .ResolveSelectedTraits()
                .Concat(recipient.Actor.Progression.ResolveSelectedTraits())
                .Where(value => value != null)
                .GroupBy(value => value.DefinitionId.Value, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            identityStates.Restore(
                identity.states
                    ?? throw new InvalidOperationException(
                        "Causal social identity state list is null."),
                traitCatalog);
            RestoreStats(offender.Actor, state["offenderStats"]);
            RestoreStats(recipient.Actor, state["recipientStats"]);
            offender.Actor.SocialMemory.RestoreSnapshot(
                Deserialize<CharacterSocialMemorySnapshot>(
                    state["offenderSocialMemory"],
                    "offender social memory"));
            recipient.Actor.SocialMemory.RestoreSnapshot(
                Deserialize<CharacterSocialMemorySnapshot>(
                    state["recipientSocialMemory"],
                    "recipient social memory"));
            DungeonCharacterBodyHealthSaveData health = Deserialize<
                DungeonCharacterBodyHealthSaveData>(
                    state["bodyHealth"],
                    "body-health state");
            bodyHealth.PublishRestore(bodyHealth.PrepareRestore(health));
            DungeonMigratedProducerOutcomeSaveData ownerData = Deserialize<
                DungeonMigratedProducerOutcomeSaveData>(
                    state["outcomeOwners"],
                    "outcome owners");
            migrated.Restore(migrated.PrepareRestore(ownerData));
            CombatActionPendingSaveData pending = Deserialize<
                CombatActionPendingSaveData>(
                    state["combatActionPending"],
                    "combat action pending");
            combatActions.PublishRestore(combatActions.PrepareRestore(pending));
            var social = Deserialize<SocialInteractionPublicationSaveData>(state["socialObservations"], "social observations");
            SocialInteractionPublicationSaveSection.ValidateJson(state["socialObservations"].ToString());
            SocialInteractionFactPreflight.ValidateJournal(social, ledger.CaptureGameplayOutcomes());
            socialOutcomes.Restore(social);

            assaultExecuted = (bool)state["assaultExecuted"];
            apologyExecuted = (bool)state["apologyExecuted"];
            if (apologyExecuted && !assaultExecuted)
            {
                throw new InvalidOperationException(
                    "Causal social owner contains an apology without its assault.");
            }
            bool canForgive = memories.CanForgive(
                recipient.Actor,
                offender.Actor,
                restitutionProvided: true);
            if (canForgive != (assaultExecuted && !apologyExecuted))
            {
                throw new InvalidOperationException(
                    "Causal social relationship memory disagrees with its execution state.");
            }
        }

        private GameplayOutcomeReplayIdentity RequireExactOutcome(
            string producerId,
            string operationId,
            GameplayOutcomeTypeId expectedType,
            bool throwOnFailure)
        {
            var key = new GameplayResultKey(
                producerId,
                new GameplayOperationId(operationId),
                0L,
                0);
            bool valid = ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact != null
                && string.Equals(
                    exact.outcomeTypeId,
                    expectedType.Value,
                    StringComparison.Ordinal);
            if (!valid && throwOnFailure)
            {
                throw new InvalidOperationException(
                    "Causal social production entry did not publish exact outcome "
                    + expectedType.Value + ".");
            }
            return valid ? identity : default;
        }

        private static JObject CaptureStats(CharacterActor actor)
        {
            CharacterMoodSnapshot mood = actor?.Mood
                ?? throw new InvalidOperationException(
                    "Causal social actor mood is unavailable.");
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
            {
                throw new InvalidOperationException(
                    "Causal social actor stats are missing.");
            }
            RequireFields(
                value,
                "causal social actor stats",
                "conditions",
                "currentHealth",
                "injurySeverity",
                "baseMood",
                "moodFactors");
            if (value["conditions"] is not JArray conditions
                || value["moodFactors"] is not JArray factors)
            {
                throw new InvalidOperationException(
                    "Causal social actor stat arrays are invalid.");
            }
            var restoredStats = new Dictionary<CharacterCondition, float>();
            foreach (JObject row in conditions.Cast<JObject>())
            {
                RequireFields(row, "causal social condition", "condition", "value");
                if (!Enum.TryParse(
                        (string)row["condition"],
                        ignoreCase: false,
                        out CharacterCondition condition)
                    || !restoredStats.TryAdd(condition, (float)row["value"]))
                {
                    throw new InvalidOperationException(
                        "Causal social condition is unknown or duplicated.");
                }
            }
            var restoredFactors = new List<CharacterMoodFactorSnapshot>();
            string previousFactor = string.Empty;
            foreach (JObject row in factors.Cast<JObject>())
            {
                RequireFields(
                    row,
                    "causal social mood factor",
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
                        "Causal social mood factors are empty, duplicated, or unordered.");
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

        private static JObject Serialize(object value, string label)
        {
            try
            {
                return JObject.Parse(JsonUtility.ToJson(
                    value ?? throw new ArgumentNullException(nameof(value))));
            }
            catch (Exception exception) when (
                exception is JsonException or ArgumentException)
            {
                throw new InvalidOperationException(
                    "Could not serialize causal social " + label + ".",
                    exception);
            }
        }

        private static T Deserialize<T>(JToken token, string label)
            where T : class
        {
            T value = JsonUtility.FromJson<T>(
                (token ?? throw new InvalidOperationException(
                    "Causal social " + label + " is missing."))
                .ToString(Formatting.None));
            return value ?? throw new InvalidOperationException(
                "Causal social " + label + " is invalid.");
        }

        private static void RequireFields(
            JObject value,
            string label,
            params string[] expected)
        {
            string[] actual = (value
                    ?? throw new ArgumentNullException(nameof(value)))
                .Properties()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            string[] wanted = expected
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    label + " fields mismatch. actual="
                    + string.Join(",", actual)
                    + "; expected="
                    + string.Join(",", wanted));
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    [Serializable]
    private sealed class IdentityStateEnvelope
    {
        public List<CharacterIdentityRuntimeStateSaveData> states = new();
    }

    private sealed class ActorFixture : IDisposable
    {
        private readonly GameObject actorObject;
        private readonly CharacterSO data;

        internal ActorFixture(
            string persistentId,
            string displayName,
            int seed,
            CharacterBodyHealthRuntime bodyHealth,
            IGameEventBus events,
            params int[] traitIds)
        {
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                displayName,
                "human");
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();
            actorObject = new GameObject(
                "SocialLifeCausalProductionEntry_" + seed);
            actorObject.AddComponent<SpriteRenderer>();
            Actor = actorObject.AddComponent<CharacterActor>();
            Actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            Actor.Identity.SetPersistentId(persistentId);
            Actor.Initialization(data);
            Actor.Progression.ApplyPreparedIdentity(
                displayName,
                "social-life-causal-production-entry",
                traitIds ?? Array.Empty<int>(),
                CharacterPotentialGrade.Ordinary,
                seed,
                autoChooseDrafts: false,
                maximumTraitCount: 4,
                ensureInitialDrafts: false);
            Actor.Stats.ConstructCharacterVitals(
                new CharacterStatsVitalsService(
                    bodyHealth,
                    bodyHealth,
                    events,
                    new CharacterDeathEventFactory(
                        CharacterAiEditorTestDependencies.WorldRegistry,
                        CharacterAiEditorTestDependencies.GameCalendar),
                    new NoopOwnerRunLifecycleService()));
            bodyHealth.ConfigureVitals(
                Actor,
                maximumHealth: 100f,
                resetCurrentHealth: true);
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
            Actor.Stats.Stats[CharacterCondition.MOOD] = 70f;
            _ = Actor.Mood;
        }

        internal CharacterActor Actor { get; }

        public void Dispose()
        {
            if (actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
            if (data != null)
                UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private sealed class FixedCharacterWorld : ICharacterWorldQuery
    {
        private readonly CharacterActor[] actors;

        internal FixedCharacterWorld(IEnumerable<CharacterActor> actors) =>
            this.actors = (actors ?? Array.Empty<CharacterActor>())
                .Where(value => value != null)
                .ToArray();

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => actors;
    }

    private sealed class NoopOwnerRunLifecycleService :
        IOwnerRunLifecycleService
    {
        public void HandleOwnerDeath(CharacterActor owner, string reason)
        {
        }
    }

    private sealed class NoCoverDurabilityRegistry :
        ICombatCoverDurabilityRegistry
    {
        public void Register(CombatCoverDurability durability)
        {
        }

        public void Unregister(CombatCoverDurability durability)
        {
        }

        public bool TryApplyDamage(string sourceId, float damage) => false;
    }

    private sealed class NoWorldUiHierarchy : IWorldUiHierarchy
    {
        public Transform GetWorldUiRoot(GameObject sceneHint = null) => null;

        public void ParentToWorldUi(GameObject child)
        {
        }
    }

    private sealed class EmptyBuildingWorld : IBuildingWorldQuery
    {
        internal static EmptyBuildingWorld Instance { get; } = new();

        private EmptyBuildingWorld()
        {
        }

        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings =>
            Array.Empty<BuildableObject>();
    }

    private sealed class EmptyNarrativeQuery : ICharacterNarrativeQuery
    {
        internal static EmptyNarrativeQuery Instance { get; } = new();

        private EmptyNarrativeQuery()
        {
        }

        public int Version => 0;
        public IReadOnlyCollection<CharacterNarrativeSnapshot> All =>
            Array.Empty<CharacterNarrativeSnapshot>();

        public bool TryGet(
            CharacterId characterId,
            out CharacterNarrativeSnapshot snapshot)
        {
            snapshot = null;
            return false;
        }

        public bool CanPerformPractice(
            CharacterId characterId,
            string practiceId,
            int absoluteDay,
            out int nextAllowedAbsoluteDay)
        {
            nextAllowedAbsoluteDay = 0;
            return false;
        }

        public bool TryPreviewAmbitionProgress(
            CharacterId characterId,
            int amount,
            out AmbitionProgressPreview preview)
        {
            preview = default;
            return false;
        }
    }

    private sealed class FixedGameClock : IGameClock
    {
        internal FixedGameClock(int absoluteDay) =>
            Time = Math.Max(0, absoluteDay)
                * GameCalendarRules.SecondsPerDay;

        public float DeltaTime => 0.02f;
        public float Time { get; }
        public int FrameCount => 1;
        public bool IsPaused => false;
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        internal FixedCalendar(int day) => Day = Math.Max(1, day);

        public int Day { get; private set; }
        public int Hour { get; private set; }
        public int Year => 1;
        public int DayOfYear => Day;
        public Season Season => default;
        public int DayOfSeason => Day;
        public long AbsoluteHour => (long)Day * 24L + Hour;
        public float ElapsedSeconds =>
            Day * GameCalendarRules.SecondsPerDay
            + Hour * (GameCalendarRules.SecondsPerDay / 24f);
        public TimeOfDay TimeOfDay => default;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);

        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() => IsRunning = true;

        public void SetDateTime(int day, int hour)
        {
            Day = Math.Max(1, day);
            Hour = Math.Clamp(hour, 0, 23);
        }
    }
}
#endif
