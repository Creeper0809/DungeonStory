#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MigratedProducerCombatDefenseEntryE2EDebugScenarios
{
    private static readonly MigratedProducerOutcomeKind[] CoveredKindArray =
    {
        MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent,
        MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent,
        MigratedProducerOutcomeKind.CharacterDeathEvent,
        MigratedProducerOutcomeKind.CharacterKilledEvent,
        MigratedProducerOutcomeKind.CombatAttackResult,
        MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent,
        MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent,
        MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent,
        MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
        MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent,
        MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent,
        MigratedProducerOutcomeKind.InvasionResolvedEvent,
        MigratedProducerOutcomeKind.InvasionStartedEvent
    };

    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds { get; }
        = Array.AsReadOnly(CoveredKindArray);

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            var observed = new HashSet<MigratedProducerOutcomeKind>();
            VerifyKinds(
                observed,
                new[]
                {
                    MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent,
                    MigratedProducerOutcomeKind.CharacterDeathEvent,
                    MigratedProducerOutcomeKind.HealthThresholdCrossedEvent
                },
                includeCombatDamage: false,
                (harness, kind) => new HealthEntryFixture(harness, kind));
            VerifyKinds(
                observed,
                new[]
                {
                    MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent,
                    MigratedProducerOutcomeKind.CharacterKilledEvent,
                    MigratedProducerOutcomeKind.CombatAttackResult
                },
                includeCombatDamage: true,
                (harness, kind) => new CombatEntryFixture(harness, kind));
            VerifyKinds(
                observed,
                new[]
                {
                    MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent,
                    MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent
                },
                includeCombatDamage: false,
                (harness, kind) => new DefenseFacilityEntryFixture(harness));
            VerifyKinds(
                observed,
                new[]
                {
                    MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent,
                    MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent,
                    MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent,
                    MigratedProducerOutcomeKind.InvasionResolvedEvent
                },
                includeCombatDamage: false,
                (harness, kind) => new InvasionIntruderEntryFixture(
                    harness,
                    kind));
            VerifyKinds(
                observed,
                new[] { MigratedProducerOutcomeKind.InvasionStartedEvent },
                includeCombatDamage: false,
                (harness, _) => new InvasionDirectorEntryFixture(harness));

            Require(
                CoveredKindArray.Distinct().Count() == CoveredKindArray.Length,
                "Combat/defense migrated-producer coverage contains duplicates.");
            Require(
                observed.Count == CoveredKindArray.Length
                && observed.SetEquals(CoveredKindArray),
                "Combat/defense migrated-producer observed coverage drifted from CoveredKinds.");
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    private static void VerifyKinds(
        ISet<MigratedProducerOutcomeKind> observed,
        IEnumerable<MigratedProducerOutcomeKind> kinds,
        bool includeCombatDamage,
        Func<OutcomeHarness, MigratedProducerOutcomeKind, IEntryFixture>
            fixtureFactory)
    {
        foreach (MigratedProducerOutcomeKind kind in kinds)
        {
            VerifyKind(kind, includeCombatDamage, fixtureFactory);
            Require(
                observed.Add(kind),
                "Migrated producer kind was exercised more than once: " + kind);
        }
    }

    private static void VerifyKind(
        MigratedProducerOutcomeKind kind,
        bool includeCombatDamage,
        Func<OutcomeHarness, MigratedProducerOutcomeKind, IEntryFixture>
            fixtureFactory)
    {
        using (var successHarness = new OutcomeHarness(
                   kind,
                   "success",
                   includeCombatDamage,
                   MigratedProducerOutcomeFaultMode.None,
                   deliveryFault: false))
        using (IEntryFixture entry = fixtureFactory(successHarness, kind))
        {
            string before = entry.CaptureDomainState();
            Require(
                Invoke(entry, out Exception failure),
                "Production entry failed in success phase for " + kind
                + FailureSuffix(failure));
            string after = entry.CaptureDomainState();
            Require(
                !string.Equals(before, after, StringComparison.Ordinal),
                "Production entry did not mutate authoritative domain state for "
                + kind);
            successHarness.RequireExact(
                kind,
                GameplayOutcomeReplayState.PublishedAcknowledged);
        }

        using (var rejectedHarness = new OutcomeHarness(
                   kind,
                   "reject",
                   includeCombatDamage,
                   MigratedProducerOutcomeFaultMode.RejectCommit,
                   deliveryFault: false))
        using (IEntryFixture entry = fixtureFactory(rejectedHarness, kind))
        {
            string domainBefore = entry.CaptureDomainState();
            string ownersBefore = rejectedHarness.CaptureOwnerState();
            bool accepted = Invoke(entry, out _);
            Require(
                !accepted,
                "Injected definite commit rejection was accepted for " + kind);
            Require(
                string.Equals(
                    domainBefore,
                    entry.CaptureDomainState(),
                    StringComparison.Ordinal),
                "Definite commit rejection did not roll domain state back for "
                + kind);
            Require(
                string.Equals(
                    ownersBefore,
                    rejectedHarness.CaptureOwnerState(),
                    StringComparison.Ordinal),
                "Definite commit rejection advanced migrated owner state for "
                + kind);
            Require(
                rejectedHarness.Probe.ReservationCount(kind) > 0
                && rejectedHarness.Probe.CommitAttemptCount(kind) > 0
                && rejectedHarness.Probe.CommitCount(kind) == 0,
                "Definite commit rejection did not cross the production reservation boundary for "
                + kind);
        }

        using (var deliveryHarness = new OutcomeHarness(
                   kind,
                   "delivery",
                   includeCombatDamage,
                   MigratedProducerOutcomeFaultMode.None,
                   deliveryFault: true))
        using (IEntryFixture entry = fixtureFactory(deliveryHarness, kind))
        {
            string before = entry.CaptureDomainState();
            Require(
                Invoke(entry, out Exception failure),
                "Production entry failed before durable delivery retry for "
                + kind + FailureSuffix(failure));
            string committed = entry.CaptureDomainState();
            Require(
                !string.Equals(before, committed, StringComparison.Ordinal),
                "Delivery-fault commit did not mutate authoritative state for "
                + kind);
            deliveryHarness.RequirePendingThenRetry(kind);
            Require(
                string.Equals(
                    committed,
                    entry.CaptureDomainState(),
                    StringComparison.Ordinal),
                "Delivery retry duplicated the domain mutation for " + kind);
            Require(
                deliveryHarness.Probe.CommitCount(kind) == 1,
                "Delivery retry crossed the domain commit boundary more than once for "
                + kind);
        }
    }

    private static bool Invoke(IEntryFixture fixture, out Exception failure)
    {
        try
        {
            failure = null;
            return fixture.InvokeProductionEntry();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            failure = exception;
            return false;
        }
    }

    private static string FailureSuffix(Exception exception) => exception == null
        ? string.Empty
        : "; " + exception.GetType().Name + ": " + exception.Message;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private interface IEntryFixture : IDisposable
    {
        string CaptureDomainState();
        bool InvokeProductionEntry();
    }

    private sealed class OutcomeHarness : IDisposable
    {
        internal OutcomeHarness(
            MigratedProducerOutcomeKind kind,
            string phase,
            bool includeCombatDamage,
            MigratedProducerOutcomeFaultMode faultMode,
            bool deliveryFault)
        {
            Fault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = kind,
                Enabled = deliveryFault
            };
            var migratedAdapter = new MigratedProducerOutcomeAdapter();
            IGameplayOutcomeDescriptor[] descriptors = includeCombatDamage
                ? new IGameplayOutcomeDescriptor[]
                {
                    new CombatDamageOutcomeDescriptor()
                }
                : Array.Empty<IGameplayOutcomeDescriptor>();
            IGameplayOutcomeAdapterRegistration[] adapters = includeCombatDamage
                ? new IGameplayOutcomeAdapterRegistration[]
                {
                    new CombatDamageOutcomeAdapter(),
                    migratedAdapter
                }
                : new IGameplayOutcomeAdapterRegistration[]
                {
                    migratedAdapter
                };
            Registry = new GameplayOutcomeRegistry(
                descriptors,
                adapters,
                new IGameplayOutcomeDescriptorCatalog[]
                {
                    new FaultingMigratedProducerOutcomeDescriptorCatalog(
                        new KoreanJosaFormatter(),
                        Fault)
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 128,
                largePageCount: 8,
                knownResultKeyCapacity: 512,
                retainedSmallPageCount: 128,
                retainedLargePageCount: 8);
            RunId = "run:migrated-entry:"
                + (int)kind + ":" + phase + ":" + Guid.NewGuid().ToString("N");
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(RunId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            AggregateRootStore = new DungeonRuntimeAggregateRootStore();
            Transaction = new MigratedProducerOutcomeRuntime(
                AggregateRootStore,
                Ledger,
                Recorder,
                Ledger);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Transaction,
                Recorder)
            {
                TargetKind = kind,
                FaultMode = faultMode
            };
        }

        internal string RunId { get; }
        internal MigratedProducerOutcomeDeliveryFault Fault { get; }
        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
        internal DungeonRuntimeAggregateRootStore AggregateRootStore { get; }
        internal MigratedProducerOutcomeRuntime Transaction { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }

        internal string CaptureOwnerState() =>
            JsonUtility.ToJson(Transaction.Capture());

        internal GameplayOutcomeReplayIdentity RequireExact(
            MigratedProducerOutcomeKind kind,
            GameplayOutcomeReplayState expectedState)
        {
            Require(
                Probe.ReservationCount(kind) > 0
                && Probe.CommitCount(kind) > 0,
                "Production entry did not reserve and commit " + kind);
            GameplayResultKey key = Probe.LatestCommittedKey(kind);
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(kind);
            GameplayOutcomeReplayIdentity identity = default;
            GameplayOutcomeSnapshot exact = default;
            bool identityFound = key.IsValid
                && Ledger.TryGetResultIdentity(key, out identity);
            bool exactFound = identityFound
                && Ledger.TryGetExact(identity.OutcomeId, out exact);
            Require(
                identityFound
                && identity.State == expectedState
                && exactFound
                && string.Equals(
                    exact.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal),
                "Production entry did not leave the exact expected ledger row for "
                + kind
                + "; keyValid=" + key.IsValid
                + "; identityFound=" + identityFound
                + "; state=" + identity.State
                + "; expectedState=" + expectedState
                + "; exactFound=" + exactFound
                + "; actualType=" + (exact?.outcomeTypeId ?? string.Empty)
                + "; expectedType=" + definition.OutcomeTypeId.Value);
            return identity;
        }

        internal void RequirePendingThenRetry(
            MigratedProducerOutcomeKind kind)
        {
            Require(
                Probe.ReservationCount(kind) > 0
                && Probe.CommitCount(kind) > 0,
                "Production entry did not durably commit pending " + kind);
            GameplayResultKey key = Probe.LatestCommittedKey(kind);
            GameplayOutcomeReplayIdentity pending = default;
            Require(
                key.IsValid
                && Ledger.TryGetResultIdentity(key, out pending)
                && pending.State
                    == GameplayOutcomeReplayState.DeliveryFaultPending,
                "Production entry did not retain the injected delivery fault for "
                + kind + "; state=" + pending.State);
            Fault.Enabled = false;
            Require(
                Recorder.RetryPendingDeliveries(32) > 0
                && Ledger.TryGetResultIdentity(
                    pending.ResultKey,
                    out GameplayOutcomeReplayIdentity replayed)
                && replayed.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged,
                "Durably committed delivery fault did not acknowledge on retry for "
                + kind);
            _ = RequireExact(
                kind,
                GameplayOutcomeReplayState.PublishedAcknowledged);
        }

        public void Dispose()
        {
        }
    }

    private sealed class HealthEntryFixture : IEntryFixture
    {
        private readonly MigratedProducerOutcomeKind kind;
        private readonly GameObject actorObject;
        private readonly CharacterSO actorData;
        private readonly CharacterBodyHealthRuntime health;
        private readonly CharacterActor actor;

        internal HealthEntryFixture(
            OutcomeHarness harness,
            MigratedProducerOutcomeKind kind)
        {
            this.kind = kind;
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "Migrated Health Entry",
                "human");
            actorObject = new GameObject("Migrated health entry fixture");
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            actor.data = actorData;
            actor.characterType = CharacterType.NPC;
            actor.Identity.SetPersistentId(
                "character:migrated-health:" + Guid.NewGuid().ToString("N"));
            var clock = new UnityGameClock();
            var events = new GameEventBus();
            health = new CharacterBodyHealthRuntime(
                CharacterAiEditorTestDependencies.WorldRegistry,
                clock,
                events,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                new ResourceAnatomyProfileCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())),
                new DungeonRuntimeAggregateRootStore());
            actor.Stats.ConstructCharacterVitals(
                new CharacterStatsVitalsService(
                    health,
                    health,
                    events,
                    new CharacterDeathEventFactory(
                        CharacterAiEditorTestDependencies.WorldRegistry,
                        CharacterAiEditorTestDependencies.GameCalendar),
                    new NoopOwnerRunLifecycleService()));
            health.ConfigureVitals(actor, 100f, resetCurrentHealth: true);
            if (kind == MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent)
                PrepareDownedRecoveryState();
            health.ConfigureOutcomeTransactions(harness.Probe);
        }

        public string CaptureDomainState() => JsonUtility.ToJson(health.Capture());

        public bool InvokeProductionEntry()
        {
            if (kind == MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent)
            {
                health.Heal(actor, 40f, stopBleeding: true);
                return !health.GetSnapshot(actor).Downed
                    && health.GetVitals(actor).CurrentHealth > 20f;
            }

            health.Kill(
                actor,
                CharacterDeathCauseCode.Combat,
                "combat:migrated-entry-e2e");
            return health.GetVitals(actor).CurrentHealth <= 0f;
        }

        public void Dispose()
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(actorData);
        }

        private void PrepareDownedRecoveryState()
        {
            health.RestoreLegacyVitalsProjection(actor, 100f, 10f, 0.9f);
            CharacterBodyHealthSnapshot source = health.GetSnapshot(actor);
            CharacterBodyPartHealthState[] parts = source.Parts
                .Select(part => new CharacterBodyPartHealthState
                {
                    bodyPart = part.bodyPart,
                    maxHealth = part.maxHealth,
                    currentHealth = part.bodyPart is CombatBodyPart.Head
                        or CombatBodyPart.Torso
                            ? part.maxHealth * 0.2f
                            : part.maxHealth,
                    bleedingPerSecond = 0f
                })
                .ToArray();
            health.ApplySnapshot(
                actor,
                new CharacterBodyHealthSnapshot(
                    parts,
                    bloodLoss: 0f,
                    suppression: 0f,
                    consciousness: 0.2f,
                    manipulation: 1f,
                    mobility: 1f,
                    downed: true),
                "migrated-entry-recovery-setup");
            Require(
                health.GetSnapshot(actor).Downed,
                "Recovery fixture did not start from authoritative downed state.");
        }
    }

    private sealed class CombatEntryFixture : IEntryFixture
    {
        private readonly MigratedProducerOutcomeKind kind;
        private readonly OutcomeHarness harness;
        private readonly List<Object> cleanup = new();
        private readonly CharacterBodyHealthRuntime health;
        private readonly CharacterActor attacker;
        private readonly CharacterActor victim;
        private readonly CombatCommandResultApplier applier;
        private readonly string operationId;

        internal CombatEntryFixture(
            OutcomeHarness harness,
            MigratedProducerOutcomeKind kind)
        {
            this.harness = harness;
            this.kind = kind;
            var clock = new UnityGameClock();
            var events = new GameEventBus();
            health = new CharacterBodyHealthRuntime(
                CharacterAiEditorTestDependencies.WorldRegistry,
                clock,
                events,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                new ResourceAnatomyProfileCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())),
                new DungeonRuntimeAggregateRootStore());
            attacker = CreateActor(
                "Migrated Combat Attacker",
                "character:migrated-combat-attacker:",
                CharacterType.NPC);
            victim = CreateActor(
                "Migrated Combat Victim",
                "character:migrated-combat-victim:",
                CharacterType.NPC);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                attacker);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                victim);
            var damageBridge = new CombatDamageOutcomeBridge(
                harness.Recorder,
                harness.Ledger);
            applier = new CombatCommandResultApplier(
                CharacterAiEditorTestDependencies.CombatEquipment,
                health,
                health,
                health,
                damageBridge,
                new NoCoverDurabilityRegistry(),
                clock,
                new NoWorldUiHierarchy(),
                events,
                CharacterAiEditorTestDependencies.WorldRegistry,
                CharacterAiEditorTestDependencies.WorldRegistry,
                new RoomFacilityPolicyService(new RoomLayoutCache()),
                CharacterAiEditorTestDependencies.GameCalendar,
                migratedOutcomes: harness.Probe,
                migratedExternalBatch: harness.Probe);
            operationId = "combat:migrated-entry:"
                + (int)kind + ":" + Guid.NewGuid().ToString("N");
        }

        public string CaptureDomainState() => JsonUtility.ToJson(health.Capture());

        public bool InvokeProductionEntry()
        {
            var target = new CombatParticipantRef(victim);
            if (!applier.TryReserveDamageOutcome(
                    target,
                    operationId,
                    1L,
                    out ReservedCombatDamageOutcome reserved,
                    out _,
                    out _))
            {
                return false;
            }
            bool downed = kind
                == MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent;
            float damage = kind == MigratedProducerOutcomeKind.CharacterKilledEvent
                || downed
                    ? 100f
                    : 5f;
            var result = new CombatAttackResult(
                executed: true,
                hit: true,
                coverBlocked: false,
                evaded: false,
                bodyPart: CombatBodyPart.Torso,
                rawDamage: damage,
                appliedDamage: damage,
                bleeding: 0f,
                suppression: 3f,
                armorDurabilityDamage: 0f,
                armorInstanceId: string.Empty,
                failureReason: string.Empty,
                nonlethal: downed);
            CombatOutcomeApplyResult applied = applier.Apply(
                target,
                result,
                attacker,
                attacker.Identity.DisplayName,
                CombatDamageType.Slash,
                operationId,
                1L,
                reserved,
                new CombatOutcomeMechanicalMutation(
                    apply: () => string.Empty,
                    rollback: () => { },
                    complete: () => { }));
            return applied.Succeeded;
        }

        public void Dispose()
        {
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(
                attacker);
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(
                victim);
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    Object.DestroyImmediate(cleanup[index]);
            }
        }

        private CharacterActor CreateActor(
            string displayName,
            string idPrefix,
            CharacterType characterType)
        {
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(characterType, displayName, "human");
            cleanup.Add(data);
            var actorObject = new GameObject(displayName);
            cleanup.Add(actorObject);
            CharacterActor result = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            result.EnsureRuntimeState();
            result.data = data;
            result.characterType = characterType;
            result.Identity.SetPersistentId(
                idPrefix + Guid.NewGuid().ToString("N"));
            result.Stats.ConstructCharacterVitals(
                new CharacterStatsVitalsService(
                    health,
                    health,
                    new GameEventBus(),
                    new CharacterDeathEventFactory(
                        CharacterAiEditorTestDependencies.WorldRegistry,
                        CharacterAiEditorTestDependencies.GameCalendar),
                    new NoopOwnerRunLifecycleService()));
            health.ConfigureVitals(result, 100f, resetCurrentHealth: true);
            return result;
        }
    }

    private sealed class DefenseFacilityEntryFixture : IEntryFixture
    {
        private readonly BuildingFixture building;
        private readonly ActorFixture target;
        private readonly DefenseFacility facility;
        private readonly DefenseFacilityRuntime runtime;
        private readonly IDefenseStatusRuntimeService statuses =
            new DefenseStatusRuntimeService(new DefenseStatusRuntimeFactory());

        internal DefenseFacilityEntryFixture(OutcomeHarness harness)
        {
            var grid = new Grid(5, 1);
            building = new BuildingFixture(
                grid,
                "Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset",
                new Vector2Int(2, 0),
                "building:migrated-defense:" + Guid.NewGuid().ToString("N"));
            facility = building.Building as DefenseFacility
                ?? throw new InvalidOperationException(
                    "Defense fixture asset did not create DefenseFacility.");
            target = new ActorFixture(
                CharacterType.Intruder,
                "Migrated Defense Target");
            var events = new GameEventBus();
            runtime = new DefenseFacilityRuntime(
                UnavailableEquipmentPhysicalItemGateway.Instance,
                new NoopDefensePhysicalGateway(),
                new AlwaysReadyDefenseInputOwner(),
                new UnityGameClock(),
                events,
                new AlwaysPoweredQuery(),
                new NoopDefenseFacilityNetwork(),
                new EmptyFacilityCapabilities(),
                new DungeonRuntimeAggregateRootStore());
            facility.ConstructDefenseFacilityEventBus(
                events,
                null,
                runtime);
            facility.ConstructDefenseFacilityOutcomeTransactions(harness.Probe);
            // A live defense facility is registered with its runtime before a
            // trigger transaction starts. Materialize that authoritative
            // baseline here so rejection compares only activation mutations,
            // not lazy owner registration performed by the first query.
            _ = runtime.GetSnapshot(facility);
        }

        public string CaptureDomainState() =>
            JsonUtility.ToJson(runtime.CaptureState())
            + "|target=" + target.CaptureHealthState();

        public bool InvokeProductionEntry() => facility.Trigger(
            target.Actor,
            DefenseTriggerTiming.OnEnter,
            statuses) != null;

        public void Dispose()
        {
            target.Dispose();
            building.Dispose();
        }
    }

    private sealed class InvasionIntruderEntryFixture : IEntryFixture
    {
        private readonly MigratedProducerOutcomeKind kind;
        private readonly GameObject intruderObject;
        private readonly InvasionIntruderRuntime runtime;
        private readonly Grid grid;
        private readonly GameEventBus events;
        private readonly BuildingFixture damageTarget;
        private readonly DefenseEngagement engagement;
        private readonly DefenseEngagementStore engagementStore;
        private readonly DefenseEngagementCombatRuntime engagementCombat;

        internal InvasionIntruderEntryFixture(
            OutcomeHarness harness,
            MigratedProducerOutcomeKind kind)
        {
            this.kind = kind;
            grid = new Grid(6, 1);
            grid.SetAreaType(new Vector2Int(1, 0), GridCellAreaType.DungeonInterior);
            events = new GameEventBus();
            intruderObject = CreatePreparedIntruderObject(
                harness.Probe,
                grid,
                events,
                out runtime);
            if (kind == MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent)
            {
                runtime.transform.position = grid.GetWorldPos(new Vector2Int(1, 0));
                damageTarget = new BuildingFixture(
                    grid,
                    "Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset",
                    new Vector2Int(2, 0),
                    "building:migrated-invasion-damage:"
                    + Guid.NewGuid().ToString("N"));
            }
            if (kind == MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent)
            {
                runtime.gameObject.SetActive(false);
                engagementStore = new DefenseEngagementStore();
                engagement = new DefenseEngagement
                {
                    Id = engagementStore.AllocateId(),
                    Intruder = runtime,
                    State = DefenseEngagementState.Engaged,
                    StatusText = "engaged"
                };
                engagementStore.Add(engagement);
                engagementCombat = new DefenseEngagementCombatRuntime(
                    engagementStore,
                    events,
                    new UnityGameClock());
            }
        }

        public string CaptureDomainState()
        {
            string buildingState = damageTarget == null
                ? string.Empty
                : ";damaged=" + damageTarget.Building.IsDamaged;
            string engagementState = engagement == null
                ? string.Empty
                : ";engagement=" + engagement.State
                    + ";stored=" + engagementStore.Engagements.Count;
            return "state=" + runtime.State
                + ";breached=" + runtime.HasBreachedDungeonInterior
                + ";damageCount=" + runtime.FacilityDamageCount
                + buildingState
                + engagementState;
        }

        public bool InvokeProductionEntry()
        {
            switch (kind)
            {
                case MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent:
                    engagementCombat.CollapseFront(
                        engagement,
                        "migrated-entry-front-collapse");
                    return engagement.State == DefenseEngagementState.Completed
                        && engagementStore.Engagements.Count == 0;
                case MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent:
                    new InvasionIntruderExecutionCoordinator(runtime)
                        .MarkDungeonBreached(grid, new Vector2Int(1, 0));
                    return runtime.HasBreachedDungeonInterior;
                case MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent:
                    return runtime.TryDamageNearbyFacility(
                        grid,
                        damageTarget.Building);
                case MigratedProducerOutcomeKind.InvasionResolvedEvent:
                    return runtime.TryCommitResolution(
                        true,
                        1f,
                        InvasionIntruderState.Finished,
                        "migrated-entry-invasion-resolved");
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public void Dispose()
        {
            damageTarget?.Dispose();
            if (intruderObject != null)
                Object.DestroyImmediate(intruderObject);
        }
    }

    private sealed class InvasionDirectorEntryFixture : IEntryFixture
    {
        private readonly GameObject directorObject;
        private readonly InvasionDirectorRuntime director;
        private readonly FixtureIntruderFactory factory;
        private readonly InvasionThreatSnapshot threat;

        internal InvasionDirectorEntryFixture(OutcomeHarness harness)
        {
            CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
                "Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset")
                ?? throw new InvalidOperationException(
                    "Authored invasion intruder fixture is missing.");
            EnemyArchetypeDefinitionSO archetype = AssetDatabase
                .LoadAssetAtPath<EnemyArchetypeDefinitionSO>(
                    "Assets/Resources/SO/V20/Combat/Enemies/enemy_neutral-mercenary.asset")
                ?? throw new InvalidOperationException(
                    "Authored enemy archetype fixture is missing.");
            var patterns = new AuthoredGameplayCatalog(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()));
            var grid = new Grid(4, 1);
            var context = new FixtureInvasionContext(grid);
            factory = new FixtureIntruderFactory(patterns);
            var individuals = new FixtureEnemyIndividuals(data, archetype);
            var events = new GameEventBus();
            directorObject = new GameObject("Migrated invasion director entry");
            director = directorObject.AddComponent<InvasionDirectorRuntime>();
            director.Construct(
                context,
                new FixedIntruderDataProvider(data),
                factory,
                new DefenseStatusRuntimeService(
                    new DefenseStatusRuntimeFactory()),
                new UnityGameClock(),
                new RandomStreamProvider(7319),
                events,
                new OffenseRegionRuntime(),
                new NoopTreasuryDefenseRuntime(),
                externalInfluence: null,
                campaignRuntime: null,
                new EmptyFacilityCapabilities(),
                new InvasionSignalHornDurableEquipmentRuntime(
                    new MissingDurableEquipmentPolicies(),
                    new MissingDurableEquipmentSlots(),
                    new MissingDurableEquipmentUse()),
                new FixedCharacterPerformance(),
                harness.Probe);
            director.ConfigureEnemyIndividuals(
                new FixtureEnemyArchetypeCatalog(archetype),
                individuals);
            threat = new InvasionThreatSnapshot(
                25f,
                InvasionThreatStage.Candidate,
                new InvasionThreatFactors(10f, 5f, 5f, 5f),
                0f,
                0f);
        }

        public string CaptureDomainState() =>
            "active=" + director.ActiveIntruders.Count
            + ";factory=" + factory.LiveCount;

        public bool InvokeProductionEntry() =>
            director.TrySpawnIntruder(threat, out CharacterActor intruder)
            && intruder != null;

        public void Dispose()
        {
            factory.Dispose();
            Object.DestroyImmediate(directorObject);
        }
    }

    private static GameObject CreatePreparedIntruderObject(
        IMigratedProducerOutcomeTransaction outcomes,
        Grid grid,
        IGameEventBus events,
        out InvasionIntruderRuntime runtime)
    {
        CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset")
            ?? throw new InvalidOperationException(
                "Authored invasion intruder fixture is missing.");
        var intruderObject = new GameObject("Migrated invasion intruder entry");
        intruderObject.AddComponent<AbilityMove>();
        intruderObject.AddComponent<CharacterActor>();
        runtime = intruderObject.AddComponent<InvasionIntruderRuntime>();
        CharacterAiEditorTestDependencies.Inject(intruderObject);
        runtime.ConfigureContent(new AuthoredGameplayCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader())));
        runtime.Initialize(
            new FixtureInvasionContext(grid),
            new DefenseStatusRuntimeService(new DefenseStatusRuntimeFactory()),
            new UnityGameClock(),
            new RandomStreamProvider(4099),
            events,
            new NoopTreasuryDefenseRuntime(),
            new FixedCharacterPerformance(),
            outcomes);
        runtime.PrepareBegin(
            data,
            new InvasionThreatSnapshot(
                20f,
                InvasionThreatStage.Candidate,
                new InvasionThreatFactors(5f, 5f, 5f, 5f),
                0f,
                0f),
            new InvasionIntruderSettings
            {
                patternId = InvasionIntruderPatternIds.Hunter,
                rallyDurationSeconds = 12f,
                facilityDamageIntervalSeconds = 0f
            },
            Vector3.zero,
            preparedRuntimeId: "invasion:migrated-entry:"
                + Guid.NewGuid().ToString("N"));
        return intruderObject;
    }

    private sealed class BuildingFixture : IDisposable
    {
        internal BuildingFixture(
            Grid grid,
            string assetPath,
            Vector2Int position,
            string persistentId)
        {
            BuildingSO data = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath)
                ?? throw new InvalidOperationException(
                    "Authored building fixture is missing: " + assetPath);
            Building = new GridBuildingFactory().Create(grid, data, position)
                ?? throw new InvalidOperationException(
                    "Building fixture could not be created: " + assetPath);
            Building.ConstructBuildableObject(
                new NoopBuildingResearchWork(),
                new NoopBuildingStateChange(),
                new NoopBuildingRoomPolicy(),
                combatEquipmentRuntime: null,
                worldRegistry: null,
                worldItemStackRuntime: null,
                abilityRuntimeDispatcher: null,
                gameClock: new UnityGameClock(),
                paidFacilityContracts: null,
                evolutionState: new FacilityEvolutionStateComponentFactory());
            Building.ConstructDebugRules(AllowBuildingDamageRule.Instance);
            Building.RestorePersistentIdentity((BuildingInstanceId)persistentId);
            Building.SetGrid(grid);
            Building.Initialization(data, position);
            if (!grid.RegisterOccupant(
                    Building,
                    data.Placement.Layer,
                    data.GetGridPosList(position),
                    data.Placement.IsMovement))
            {
                throw new InvalidOperationException(
                    "Building fixture could not register on its production grid.");
            }
        }

        internal BuildableObject Building { get; }

        public void Dispose()
        {
            if (Building != null)
                Object.DestroyImmediate(Building.gameObject);
        }
    }

    private sealed class ActorFixture : IDisposable
    {
        private readonly CharacterSO data;
        private readonly GameObject actorObject;
        private readonly CharacterBodyHealthRuntime health;

        internal ActorFixture(CharacterType type, string displayName)
        {
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                type,
                displayName,
                "human");
            actorObject = new GameObject(displayName);
            Actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            Actor.EnsureRuntimeState();
            Actor.data = data;
            Actor.characterType = type;
            Actor.Identity.SetPersistentId(
                "character:migrated-defense-target:"
                + Guid.NewGuid().ToString("N"));
            var clock = new UnityGameClock();
            var events = new GameEventBus();
            health = new CharacterBodyHealthRuntime(
                CharacterAiEditorTestDependencies.WorldRegistry,
                clock,
                events,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                new ResourceAnatomyProfileCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())),
                new DungeonRuntimeAggregateRootStore());
            Actor.Stats.ConstructCharacterVitals(
                new CharacterStatsVitalsService(
                    health,
                    health,
                    events,
                    new CharacterDeathEventFactory(
                        CharacterAiEditorTestDependencies.WorldRegistry,
                        CharacterAiEditorTestDependencies.GameCalendar),
                    new NoopOwnerRunLifecycleService()));
            health.ConfigureVitals(Actor, 100f, resetCurrentHealth: true);
        }

        internal CharacterActor Actor { get; }
        internal string CaptureHealthState() => JsonUtility.ToJson(health.Capture());

        public void Dispose()
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(data);
        }
    }

    private sealed class FixtureInvasionContext : IInvasionIntruderContext
    {
        private readonly Grid grid;

        internal FixtureInvasionContext(Grid grid) =>
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));

        public bool TryGetGrid(out Grid value)
        {
            value = grid;
            return true;
        }

        public bool TryGetOwner(out CharacterActor owner)
        {
            owner = null;
            return false;
        }

        public bool TryResolveBuilding(
            BuildingInstanceId id,
            out BuildableObject building)
        {
            building = null;
            return false;
        }

        public bool TryResolveEntry(out InvasionIntruderEntry entry)
        {
            entry = new InvasionIntruderEntry(
                Vector2Int.zero,
                new Vector3(-1f, 0f, 0f),
                Vector3.zero);
            return true;
        }

        public InvasionIntruderSettings ApplyRunVariables(
            InvasionIntruderSettings source) => source;
    }

    private sealed class FixtureIntruderFactory : IInvasionIntruderFactory,
        IDisposable
    {
        private readonly IInvasionIntruderPatternDefinitionCatalog patterns;
        private readonly List<InvasionIntruderRuntime> live = new();

        internal FixtureIntruderFactory(
            IInvasionIntruderPatternDefinitionCatalog patterns) =>
            this.patterns = patterns
                ?? throw new ArgumentNullException(nameof(patterns));

        internal int LiveCount => live.Count(value => value != null);

        public InvasionIntruderRuntime Create(
            GameObject intruderPrefab,
            Vector3 position) => CreateRuntime(position);

        public InvasionIntruderRuntime CreateDetached(
            GameObject intruderPrefab,
            Vector3 position) => CreateRuntime(position);

        public void Publish(InvasionIntruderRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            runtime.gameObject.SetActive(true);
        }

        public void PublishDetached(InvasionIntruderRuntime runtime) =>
            Publish(runtime);

        public void DestroyDetached(InvasionIntruderRuntime runtime)
        {
            live.Remove(runtime);
            if (runtime != null)
                Object.DestroyImmediate(runtime.gameObject);
        }

        public InvasionIntruderRuntime EnsureRuntime(GameObject intruderObject)
        {
            if (intruderObject == null)
                throw new ArgumentNullException(nameof(intruderObject));
            InvasionIntruderRuntime runtime =
                intruderObject.GetComponent<InvasionIntruderRuntime>()
                ?? intruderObject.AddComponent<InvasionIntruderRuntime>();
            runtime.ConfigureContent(patterns);
            if (!live.Contains(runtime))
                live.Add(runtime);
            return runtime;
        }

        public void Dispose()
        {
            for (int index = live.Count - 1; index >= 0; index--)
            {
                if (live[index] != null)
                    Object.DestroyImmediate(live[index].gameObject);
            }
            live.Clear();
        }

        private InvasionIntruderRuntime CreateRuntime(Vector3 position)
        {
            var value = new GameObject("Migrated director intruder");
            value.transform.position = position;
            value.AddComponent<AbilityMove>();
            value.AddComponent<CharacterActor>();
            InvasionIntruderRuntime runtime =
                value.AddComponent<InvasionIntruderRuntime>();
            CharacterAiEditorTestDependencies.Inject(value);
            runtime.ConfigureContent(patterns);
            live.Add(runtime);
            return runtime;
        }
    }

    private sealed class FixedIntruderDataProvider :
        IInvasionIntruderDataProvider
    {
        private readonly CharacterSO data;

        internal FixedIntruderDataProvider(CharacterSO data) =>
            this.data = data ?? throw new ArgumentNullException(nameof(data));

        public CharacterSO GetRequiredIntruderData(CharacterSO configuredData) =>
            data;
    }

    private sealed class FixtureEnemyArchetypeCatalog : IEnemyArchetypeCatalog
    {
        private readonly EnemyArchetypeDefinitionSO archetype;

        internal FixtureEnemyArchetypeCatalog(
            EnemyArchetypeDefinitionSO archetype)
        {
            this.archetype = archetype
                ?? throw new ArgumentNullException(nameof(archetype));
            All = new[] { archetype };
        }

        public IReadOnlyList<EnemyArchetypeDefinitionSO> All { get; }

        public bool TryGet(
            string id,
            out EnemyArchetypeDefinitionSO definition)
        {
            definition = string.Equals(
                id,
                archetype.stableId,
                StringComparison.Ordinal)
                    ? archetype
                    : null;
            return definition != null;
        }

        public EnemyArchetypeDefinitionSO Require(string id) =>
            TryGet(id, out EnemyArchetypeDefinitionSO value)
                ? value
                : throw new KeyNotFoundException(id);
    }

    private sealed class FixtureEnemyIndividuals : IEnemyIndividualFactory
    {
        private readonly CharacterSO data;
        private readonly EnemyArchetypeDefinitionSO archetype;

        internal FixtureEnemyIndividuals(
            CharacterSO data,
            EnemyArchetypeDefinitionSO archetype)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.archetype = archetype
                ?? throw new ArgumentNullException(nameof(archetype));
        }

        public EnemyIndividualSaveData Create(
            string enemyArchetypeId,
            CharacterId characterId,
            string deterministicContext) => new()
        {
            characterId = characterId.Value,
            enemyArchetypeId = enemyArchetypeId,
            originFactionId = archetype.factionId,
            phenotypeSpeciesId = data.speciesTag,
            displayName = "Migrated Entry Intruder",
            combatStatMultiplier = 1f
        };

        public EnemyIndividualBlueprint RequireBlueprint(
            EnemyIndividualSaveData value) => new(
            value.Clone(),
            archetype,
            CharacterSpawnRequest.FromAuthoring(data));

        public void EnsureCharacterDomains(EnemyIndividualBlueprint blueprint)
        {
        }
    }

    private sealed class FixedCharacterPerformance : ICharacterPerformanceQuery
    {
        public CharacterFunctionalCapacitySnapshot GetFunctionalCapacities(
            CharacterActor actor) => default;

        public CharacterPerformanceSnapshot Evaluate(
            CharacterActor actor,
            string formulaId,
            float contextFactor = 1f,
            GameplayEffectContext effectContext = null) => default;

        public CharacterPerformanceSnapshot Evaluate(
            CharacterActor actor,
            string formulaId,
            CharacterPerformanceEvaluationContext context) => default;

        public CharacterPerformanceSnapshot EvaluateWork(
            CharacterActor actor,
            WorkTypeId workTypeId,
            CharacterPerformanceResultChannel resultChannel,
            CharacterPerformanceEvaluationContext context) => default;

        public IReadOnlyList<CharacterPerformanceSnapshot> EvaluateDomain(
            CharacterActor actor,
            CharacterPerformanceFormulaDomain domain) =>
            Array.Empty<CharacterPerformanceSnapshot>();
    }

    private sealed class NoopTreasuryDefenseRuntime : ITreasuryDefenseRuntime
    {
        public IReadOnlyList<TreasuryDefensePolicy> Policies =>
            Array.Empty<TreasuryDefensePolicy>();
        public TreasuryDefensePolicy GetPolicy(DefenseFacility facility) => new();
        public void UpsertPolicy(TreasuryDefensePolicy policy) { }
        public TreasuryDefenseAuthorization AuthorizeShot(
            DefenseFacility facility,
            CharacterActor intruder,
            string invasionId,
            float threat,
            bool isBoss) => new(true, false, 1f, 0, string.Empty);
        public int GetSpent(string invasionId, string facilityPersistentId) => 0;
        public string GetLastFailureReason(string facilityPersistentId) =>
            string.Empty;
        public TreasuryDefenseSaveData Capture() => new();
        public void Restore(TreasuryDefenseSaveData saveData) { }
    }

    private sealed class EmptyFacilityCapabilities : IFacilityCapabilityQuery
    {
        public IReadOnlyList<BuildableObject> FindOperational(
            FacilityCapabilityKind capability,
            string buildingDefinitionId = "") =>
            Array.Empty<BuildableObject>();

        public IReadOnlyList<BuildableObject> FindOperational(
            ResearchFacilityCommandKind command) =>
            Array.Empty<BuildableObject>();
    }

    private sealed class MissingDurableEquipmentPolicies :
        IDurableFacilityEquipmentPolicyQuery
    {
        public long Revision => 0L;
        public bool TryGetPolicy(
            string policyId,
            out DurableFacilityEquipmentPolicy policy)
        {
            policy = null;
            return false;
        }
        public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
            Array.Empty<DurableFacilityEquipmentPolicy>();
    }

    private sealed class MissingDurableEquipmentSlots :
        IDurableFacilityEquipmentSlotCommand
    {
        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired) => default;
        public DurableFacilityEquipmentSlotResult TryClose(
            DurableFacilityEquipmentSlotKey key,
            string reasonCode) => default;
        public DurableFacilityEquipmentSlotResult TryEnsureSupply(
            DurableFacilityEquipmentSlotKey key) => default;
        public IReadOnlyList<DurableFacilityEquipmentSlotResult>
            TryAdvancePending() =>
            Array.Empty<DurableFacilityEquipmentSlotResult>();
    }

    private sealed class MissingDurableEquipmentUse :
        IDurableFacilityEquipmentUseCommand
    {
        public DurableFacilityEquipmentUseResult TryApplyWearAndEffect(
            DurableFacilityEquipmentSlotKey key,
            string requirementId,
            double wearAmount,
            IDurableFacilityEquipmentEffectCommit effect) => default;
    }

    private sealed class NoopDefensePhysicalGateway :
        IDefenseFacilityPhysicalItemGateway
    {
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            Array.Empty<WorldItemStackSnapshot>();
        public bool TryCommitPendingBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = "unavailable";
            return false;
        }
        public bool TryGetPendingBatchPhysicalDisposition(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt)
        {
            receipt = default;
            return false;
        }
        public bool AcknowledgeBatchPhysicalDisposition(
            string commitId,
            out string failureReason)
        {
            failureReason = "unavailable";
            return false;
        }
    }

    private sealed class AlwaysReadyDefenseInputOwner :
        IDefenseFacilityInputOwnerRuntime
    {
        public bool TryEnsureAuthority(
            DefenseFacility facility,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
        public bool TryReconcileLive(out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
        public bool TryReconcileRestore(
            IReadOnlyCollection<DefenseFacilityState> restoredStates,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class AlwaysPoweredQuery : IPowerInfrastructureQuery
    {
        public int Version => 0;
        public IReadOnlyList<PowerNetworkSnapshot> Networks =>
            Array.Empty<PowerNetworkSnapshot>();
        public bool IsPowered(BuildableObject building) => true;
        public bool TryGetNode(
            BuildableObject building,
            out PowerNodeSnapshot snapshot)
        {
            snapshot = null;
            return false;
        }
    }

    private sealed class NoopDefenseFacilityNetwork :
        IDefenseFacilityNetworkRuntime
    {
        public int Version => 0;
        public DefenseFacilityNetworkSnapshot GetSnapshot(
            DefenseFacility facility) => new(
            facility,
            null,
            null,
            null,
            null,
            true);
        public bool HasAutomaticControl(DefenseFacility facility) => true;
        public bool HasMaintenanceCoverage(DefenseFacility facility) => false;
        public bool HasSupplyCoverage(DefenseFacility facility) => false;
        public void DetectIntruder(
            string raidId,
            Vector2Int position,
            IDefenseRaidAwarenessRuntime awareness) { }
    }

    private sealed class NoopBuildingResearchWork : IBuildingResearchWorkPort
    {
        public bool HasResearchWorkFor(IBuildingWorldEntryPort facility) => false;
    }

    private sealed class NoopBuildingStateChange :
        IBuildingFacilityStateChangePort
    {
        public void MarkDynamicStateDirty() { }
    }

    private sealed class NoopBuildingRoomPolicy : IBuildingRoomPolicyPort
    {
        public bool IsFacilityRoleAvailable(
            IBuildingWorldEntryPort building,
            FacilityRole requestedRole,
            out string rejectReason)
        {
            rejectReason = string.Empty;
            return true;
        }
        public float GetRoomUtilityScore(
            IBuildingWorldEntryPort building,
            FacilityRole role) => 1f;
        public int GetEffectiveCapacity(IBuildingWorldEntryPort building) => 1;
        public BuildingRoomOperationalSnapshot GetOperationalProfile(
            IBuildingWorldEntryPort building) => new(
            Array.Empty<IBuildingWorldEntryPort>(),
            hasRoom: false,
            isUsableRoom: true,
            qualityScore: 1f,
            seatCapacity: 0,
            tableCapacity: 0,
            serviceCapacity: 1,
            StockCategory.General,
            new Dictionary<StockCategory, int>());
    }

    private sealed class AllowBuildingDamageRule : IBuildingDamageRulePort
    {
        internal static readonly AllowBuildingDamageRule Instance = new();

        public bool ShouldBlockFacilityDamage(bool damaged) => false;
    }

    private sealed class NoopOwnerRunLifecycleService :
        IOwnerRunLifecycleService
    {
        public void HandleOwnerDeath(CharacterActor owner, string reason) { }
    }

    private sealed class NoCoverDurabilityRegistry :
        ICombatCoverDurabilityRegistry
    {
        public void Register(CombatCoverDurability durability) { }
        public void Unregister(CombatCoverDurability durability) { }
        public bool TryApplyDamage(string sourceId, float damage) => false;
    }

    private sealed class NoWorldUiHierarchy : IWorldUiHierarchy
    {
        public Transform GetWorldUiRoot(GameObject sceneHint = null) => null;
        public void ParentToWorldUi(GameObject child) { }
    }
}
#endif
