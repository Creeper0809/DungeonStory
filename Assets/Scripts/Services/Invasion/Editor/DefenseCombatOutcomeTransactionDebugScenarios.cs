using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
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
        GameplayOutcomeRegistry registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new CombatDamageOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new CombatDamageOutcomeAdapter()
            });
        GameplayOutcomeBufferLimits limits = new GameplayOutcomeBufferLimits(
            smallPageCount: 4,
            largePageCount: 0,
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
        CombatDamageOutcomeBridge bridge = new CombatDamageOutcomeBridge(
            recorder,
            ledger);
        CharacterBodyHealthRuntime health =
            CharacterAiEditorTestDependencies.BodyHealthRuntime;
        CombatCommandResultApplier applier = new CombatCommandResultApplier(
            CharacterAiEditorTestDependencies.CombatEquipment,
            health,
            health,
            health,
            bridge,
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
                out ReservedCombatDamageOutcome failedReservation,
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
                && failed.FailureReason == "injected-defense-mechanical-fault",
            "Injected defense mechanical fault was not surfaced.");
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
                out ReservedCombatDamageOutcome retryReservation,
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
        Require(operation.Items.Count == 1
                && operation.Items[0].Exact != null
                && operation.Items[0].Exact.outcomeTypeId
                    == ProductionCombatOutcomeIds.CombatDamageResolved.Value,
            "Committed defense outcome was not published exactly once.");
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
                && duplicateReason.Contains(
                    OutcomePrepareCode.DuplicateResult.ToString(),
                    StringComparison.Ordinal),
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
