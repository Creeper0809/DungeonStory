using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class DefenseCombatOutcomeTransactionDebugScenarios
{
    public static void RunAll()
    {
        List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        CharacterActor attacker = null;
        CharacterActor defender = null;
        try
        {
            attacker = CreateActor(
                "방어 공격자",
                "character:defense-outcome-attacker",
                cleanup);
            defender = CreateActor(
                "방어 대상",
                "character:defense-outcome-defender",
                cleanup);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(attacker);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(defender);
            VerifyRollbackRetryAndOrigin(attacker, defender);
            Debug.Log("DEFENSE_COMBAT_TRANSACTION=PASS; rollback=True; retry=True; origin=True; duplicateRejected=True");
        }
        finally
        {
            if (attacker != null)
            {
                CharacterAiEditorTestDependencies.WorldRegistry
                    .UnregisterCharacter(attacker);
            }
            if (defender != null)
            {
                CharacterAiEditorTestDependencies.WorldRegistry
                    .UnregisterCharacter(defender);
            }
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
        }
    }

    private static void VerifyRollbackRetryAndOrigin(
        CharacterActor attacker,
        CharacterActor defender)
    {
        GameEventBus events = new GameEventBus();
        var migratedAdapter = new MigratedProducerOutcomeAdapter();
        GameplayOutcomeRegistry registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new CombatDamageOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new CombatDamageOutcomeAdapter(),
                migratedAdapter
            },
            new IGameplayOutcomeDescriptorCatalog[]
            {
                new MigratedProducerOutcomeDescriptorCatalog(
                    new KoreanJosaFormatter())
            });
        GameplayOutcomeBufferLimits limits = new GameplayOutcomeBufferLimits(
            smallPageCount: 4,
            largePageCount: 4,
            knownResultKeyCapacity: 32);
        GameplayOutcomeLedger ledger = new GameplayOutcomeLedger(
            registry,
            limits,
            new GameplayOutcomeRunId("run:defense-combat-outcome"),
            1L);
        GameplayOutcomeRecorder recorder = new GameplayOutcomeRecorder(
            ledger,
            registry,
            events);
        var roots = new DungeonRuntimeAggregateRootStore();
        var migrated = new MigratedProducerOutcomeRuntime(
            roots,
            ledger,
            recorder,
            ledger);
        using var facts = new DomainFactDispatcher();
        facts.Subscribe(
            "log:combat.action",
            new CombatActionFactObserver(
                ledger,
                recorder,
                migrated,
                new GameplayDomainFactCommitGroup(recorder, ledger)));
        var publications = new CombatActionPublicationRuntime(
            new DungeonStory.Foundation.UnityUiClock(),
            roots,
            facts,
            facts);
        CharacterBodyHealthRuntime health =
            CharacterAiEditorTestDependencies.BodyHealthRuntime;
        CombatCommandResultApplier applier = new CombatCommandResultApplier(
            CharacterAiEditorTestDependencies.CombatEquipment,
            health,
            health,
            health,
            publications,
            new NoCoverDurabilityRegistry(),
            CharacterAiEditorTestDependencies.GameClock,
            new NoWorldUiHierarchy(),
            events,
            CharacterAiEditorTestDependencies.WorldRegistry,
            CharacterAiEditorTestDependencies.WorldRegistry,
            new RoomFacilityPolicyService(RoomRegistry.EditorCache),
            CharacterAiEditorTestDependencies.GameCalendar,
            new CharacterIdentityEventPublisher(events));

        const string operationId = "defense-combat-outcome-attack-1";
        const long revision = 0L;
        CombatParticipantRef target = new CombatParticipantRef(defender);
        CombatAttackResult result = new CombatAttackResult(
            executed: true,
            hit: true,
            coverBlocked: false,
            evaded: false,
            bodyPart: CombatBodyPart.Torso,
            rawDamage: 5f,
            appliedDamage: 5f,
            bleeding: 0f,
            suppression: 0f,
            armorDurabilityDamage: 0f,
            armorInstanceId: string.Empty,
            failureReason: string.Empty);
        float healthBefore = health.GetVitals(defender).CurrentHealth;
        Require(healthBefore > result.AppliedDamage + 1f,
            "Defense outcome fixture does not have enough health for a nonfatal hit.");

        Require(applier.TryReserveDamageOutcome(
                target,
                operationId,
                revision,
                out ReservedCombatAction failedReservation,
                out _,
                out string failedReserveReason),
            "Initial defense outcome reservation failed: " + failedReserveReason);
        bool failureApplied = false;
        bool failureRolledBack = false;
        bool failureCompleted = false;
        CombatOutcomeApplyResult failed = applier.Apply(
            target,
            result,
            attacker,
            attacker.Identity.DisplayName,
            CombatDamageType.Slash,
            operationId,
            revision,
            failedReservation,
            new CombatOutcomeMechanicalMutation(
                apply: () =>
                {
                    failureApplied = true;
                    return "injected-defense-mechanical-fault";
                },
                rollback: () => failureRolledBack = true,
                complete: () => failureCompleted = true),
            "던전 방어 교전: 방어 공격자",
            CharacterCommandOrigin.Autonomous);
        Require(!failed.Succeeded
                && failed.FailureReason == "combat-action-failed:injected-defense-mechanical-fault",
            "Injected defense mechanical fault was not surfaced: " + failed.FailureReason);
        Require(failureApplied && failureRolledBack && !failureCompleted,
            "Failed defense mutation did not follow apply/rollback semantics.");
        Require(Mathf.Approximately(
                health.GetVitals(defender).CurrentHealth,
                healthBefore),
            "Failed defense outcome left body-health mutation behind.");
        Require(ledger.GetForOperation(new GameplayOperationId(operationId))
                .Items.Count == 0,
            "Failed defense outcome leaked a ledger record.");
        Require(ledger.GetOutboxSnapshot().Count == 0,
            "Failed defense outcome leaked a durable outbox entry.");

        int conflictCount = 0;
        CharacterCommandOrigin conflictOrigin = CharacterCommandOrigin.DirectPlayerOrder;
        string conflictOperationId = string.Empty;
        using IDisposable subscription = events.Subscribe<SocialConflictEvent>(value =>
        {
            conflictCount++;
            conflictOrigin = value.Origin;
            conflictOperationId = value.OperationId;
        });
        Require(applier.TryReserveDamageOutcome(
                target,
                operationId,
                revision,
                out ReservedCombatAction retryReservation,
                out _,
                out string retryReserveReason),
            "Retry defense outcome reservation failed: " + retryReserveReason);
        bool retryApplied = false;
        bool retryRolledBack = false;
        bool retryCompleted = false;
        CombatOutcomeApplyResult committed = applier.Apply(
            target,
            result,
            attacker,
            attacker.Identity.DisplayName,
            CombatDamageType.Slash,
            operationId,
            revision,
            retryReservation,
            new CombatOutcomeMechanicalMutation(
                apply: () =>
                {
                    retryApplied = true;
                    return string.Empty;
                },
                rollback: () => retryRolledBack = true,
                complete: () => retryCompleted = true),
            "던전 방어 교전: 방어 공격자",
            CharacterCommandOrigin.Autonomous);
        Require(committed.Succeeded,
            "Retry defense outcome did not commit: " + committed.FailureReason);
        Require(retryApplied && !retryRolledBack && retryCompleted,
            "Successful defense mutation did not complete exactly once.");
        Require(Mathf.Approximately(
                health.GetVitals(defender).CurrentHealth,
                healthBefore - result.AppliedDamage),
            "Committed defense outcome applied the wrong health delta.");
        GameplayOutcomeQueryPage operation = ledger.GetForOperation(
            new GameplayOperationId(operationId));
        // The combat authority owns the damage operation; the observer assigns
        // the attack its own durable identity and links it to the domain commit.
        GameplayResultKey attackKey = MigratedProducerOutcomeCatalog.ResultKey(
            MigratedProducerOutcomeKind.CombatAttackResult, retryReservation.Proposal.Identity, 1);
        GameplayOutcomeQueryPage attack = ledger.GetForOperation(attackKey.OperationId);
        Require(ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 2
                && operation.Items.Count == 1
                && operation.Items.Count(value => value.Exact != null
                    && value.Exact.outcomeTypeId
                        == ProductionCombatOutcomeIds.CombatDamageResolved.Value) == 1
                && attack.Items.Count == 1
                && attack.Items.Count(value => value.Exact != null
                    && value.Exact.outcomeTypeId == MigratedProducerOutcomeCatalog.Get(
                            MigratedProducerOutcomeKind.CombatAttackResult)
                        .OutcomeTypeId.Value
                    && value.Exact.provenance.Count(reference => reference.kindId == "domain-commit"
                        && reference.value == operationId) == 1) == 1,
            "Committed defense action did not publish exactly one attack and one damage row.");
        Require(ledger.GetOutboxSnapshot().Count == 0,
            "Committed defense outcome was not delivered and acknowledged.");
        Require(conflictCount == 1
                && conflictOrigin == CharacterCommandOrigin.Autonomous
                && conflictOperationId == operationId,
            "Defense observer event did not retain autonomous origin and operation ID.");

        float healthAfterCommit = health.GetVitals(defender).CurrentHealth;
        Require(!applier.TryReserveDamageOutcome(
                target,
                operationId,
                revision,
                out _,
                out _,
                out string duplicateReason)
                && duplicateReason.Contains("already-applied", StringComparison.Ordinal),
            "Committed defense outcome did not reject a duplicate result key.");
        Require(Mathf.Approximately(
                health.GetVitals(defender).CurrentHealth,
                healthAfterCommit),
            "Duplicate defense attempt changed body health.");
    }

    private static CharacterActor CreateActor(
        string displayName,
        string persistentId,
        ICollection<UnityEngine.Object> cleanup)
    {
        CharacterSO data = CharacterAiEditorTestDependencies
            .CreateCharacterFixtureData(
                CharacterType.Customer,
                displayName,
                "human");
        cleanup.Add(data);
        GameObject actorObject = new GameObject(displayName);
        cleanup.Add(actorObject);
        CharacterActor actor = actorObject.AddComponent<CharacterActor>();
        CharacterAiEditorTestDependencies.Inject(actorObject);
        actor.EnsureRuntimeState();
        actor.data = data;
        actor.characterType = CharacterType.Customer;
        actor.Identity.SetPersistentId(persistentId);
        actor.stats = new Dictionary<CharacterCondition, float>
        {
            { CharacterCondition.SLEEP, 100f },
            { CharacterCondition.HUNGER, 100f },
            { CharacterCondition.FUN, 100f },
            { CharacterCondition.MOOD, 100f },
            { CharacterCondition.EXCRETION, 100f },
            { CharacterCondition.HYGIENE, 100f }
        };
        return actor;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
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
}
