using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

public interface IAgeConditionAnatomyProjectionCommand
{
    string Enqueue(in AgeConditionChange change, int absoluteDay, in DomainFactDelivery parent);
    bool TryResume(string operationId, out string failureCode);
    void ResumePendingRequired();
}

public sealed class AgeConditionAnatomyProjectionRuntime :
    IAgeConditionAnatomyProjectionCommand, IStartable, IDisposable, IDungeonSaveRestoreCompletedHook
{
    private readonly IAgeConditionAnatomyProjectionStore store;
    private readonly ICharacterLifeDefinitionCatalog definitions;
    private readonly ICharacterWorldQuery world;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IAgeConditionAnatomyMutationTransaction bodyMutations;
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IGameEventBus events;
    private readonly HashSet<string> active = new(StringComparer.Ordinal);
    private IDisposable dayEndedSubscription;

    public AgeConditionAnatomyProjectionRuntime(IAgeConditionAnatomyProjectionStore store,
        ICharacterLifeDefinitionCatalog definitions, ICharacterWorldQuery world, IAnatomyHealthRuntime anatomy,
        IAgeConditionAnatomyMutationTransaction bodyMutations, IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher recovery, IGameEventBus events)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.bodyMutations = bodyMutations ?? throw new ArgumentNullException(nameof(bodyMutations));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }
    public void Start()
    {
        dayEndedSubscription ??= events.Subscribe<OperatingDayEndedEvent>(_ => ResumeAll());
        ResumeAll();
    }
    public void Dispose() { dayEndedSubscription?.Dispose(); dayEndedSubscription = null; }
    public void OnRestoreCompleted() => ResumeAll();
    public string Enqueue(in AgeConditionChange change, int absoluteDay, in DomainFactDelivery parent)
    {
        var projection = CharacterLifeObservationState.Projection(
            CharacterLifeTransitionFact.FromAgeCondition(change, absoluteDay), parent);
        store.EnqueueAgeConditionAnatomyProjection(projection);
        return projection.operationId;
    }
    public void ResumePendingRequired()
    {
        var failures = new List<string>();
        foreach (var projection in store.PendingAgeConditionAnatomyProjections)
            if (!TryResume(projection.operationId, out string failure)) failures.Add(failure);
        if (failures.Count > 0) throw new InvalidOperationException("age-anatomy-pending:" + string.Join(";", failures));
    }
    private void ResumeAll()
    {
        foreach (var projection in store.PendingAgeConditionAnatomyProjections)
        {
            try { if (!TryResume(projection.operationId, out string failure)) Debug.LogError("Age-condition projection pending: " + failure); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
    public bool TryResume(string operationId, out string failureCode)
    {
        if (!active.Add(operationId)) { failureCode = "age-condition-operation-busy"; return false; }
        try { return Resume(operationId, out failureCode); }
        finally { active.Remove(operationId); }
    }
    private bool Resume(string operationId, out string failureCode)
    {
        var projection = store.PendingAgeConditionAnatomyProjections.SingleOrDefault(value => value.operationId == operationId);
        if (projection == null) { failureCode = "age-condition-anatomy-projection-missing"; return false; }
        CharacterActor actor = world.Characters.SingleOrDefault(candidate => candidate != null
            && CharacterPersistentIdentity.TryGet(candidate, out CharacterId id) && id.Value == projection.characterId);
        CharacterPreparedAgeConditionAnatomyReceipt mutation = default;
        bool appliedNow = false, publicationCrossed = false;
        DomainFactLease lease = default;
        try
        {
            if (projection.phase == (int)AgeConditionAnatomyProjectionPhase.Pending)
            {
                if (actor == null) return Fail(projection, "age-condition-actor-unavailable", out failureCode);
                if (actor.IsDead) return Fail(projection, "age-condition-actor-dead", out failureCode);
                var definition = definitions.RequireAgeCondition(projection.conditionId);
                var snapshot = anatomy.GetAnatomySnapshot(actor);
                string[] nodes = definition.AffectedAnatomyNodeIds.Where(id =>
                    snapshot.Nodes.Any(node => node != null && !node.missing && node.nodeId == id))
                    .Distinct(StringComparer.Ordinal).ToArray();
                if (nodes.Length == 0) return Fail(projection, "age-condition-anatomy-node-unavailable", out failureCode);
                var severity = (AgeConditionSeverity)projection.currentSeverity;
                mutation = bodyMutations.ApplyPreparedAgeConditionAnatomy(actor, nodes, DamageFraction(severity),
                    severity == AgeConditionSeverity.OrganFunctionLoss, "age-condition:" + projection.conditionId + ":" + severity);
                appliedNow = true;
                var deltas = mutation.NodeDeltas.Select(row => new AgeConditionAnatomyNodeDeltaSaveData
                { nodeId = row.NodeId, maximumHealth = row.MaximumHealth, healthBefore = row.HealthBefore,
                    requestedDamage = row.RequestedDamage, healthAfter = row.HealthAfter,
                    bleedingBefore = row.BleedingBefore, bleedingAfter = row.BleedingAfter }).ToArray();
                store.RecordAgeConditionAnatomyApplied(operationId, deltas, mutation.AggregateMaximumHealth,
                    mutation.AggregateHealthBefore, mutation.AggregateHealthAfter, mutation.AggregateWasDowned,
                    actor.IsDead, actor.Identity?.DisplayName ?? actor.name);
                projection = store.PendingAgeConditionAnatomyProjections.Single(row => row.operationId == operationId);
            }
            else mutation = RestoreMutationReceipt(projection);
            var fact = new AgeConditionAnatomyAppliedFact(projection);
            DomainFactDelivery delivery;
            if (projection.publicationKey.Length > 0)
            {
                publicationCrossed = true;
                var deliveries = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(projection.publicationKey, fact) }, deliveries, out string failure))
                    return Fail(projection, "age-condition-observation-pending:" + failure, out failureCode);
                delivery = deliveries[0];
            }
            else
            {
                if (!facts.TryReserve(fact, out lease, out string failure))
                    return RollbackOrRetain(actor, projection, mutation, appliedNow,
                        "age-condition-observation-reserve:" + failure, out failureCode);
                string key = lease.PublicationKey;
                store.RecordAgeConditionAnatomyPublication(operationId, key);
                projection.publicationKey = key;
                publicationCrossed = true;
                delivery = facts.Publish(lease, fact);
                if (!delivery.MayBeCommitted)
                {
                    publicationCrossed = false;
                    return RollbackOrRetain(actor, projection, mutation, appliedNow,
                        "age-condition-observation-rejected:" + delivery.Detail, out failureCode);
                }
            }
            if (!delivery.Completed)
                return Fail(projection, "age-condition-observation-pending:" + delivery.Detail, out failureCode);
            CharacterLifeObservationState.RequireDelivery(delivery, projection.publicationKey);
            if (actor == null) return Fail(projection, "age-condition-lifecycle-actor-unavailable", out failureCode);
            if (!actor.IsDead) bodyMutations.CompletePreparedAgeConditionAnatomy(actor, mutation);
            store.CompleteAgeConditionAnatomyProjection(operationId);
            failureCode = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        {
            if (!publicationCrossed && appliedNow && mutation.Before.IsValid)
            {
                bodyMutations.RestorePreparedAgeConditionAnatomy(actor, mutation.Before, "age-condition-anatomy-exception-rollback");
                store.ResetAgeConditionAnatomyProjection(operationId);
            }
            store.RecordAgeConditionAnatomyFailure(operationId, "age-condition-anatomy-exception:" + exception.GetType().Name);
            throw;
        }
        finally
        {
            try { facts.Cancel(lease); }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
            { Debug.LogException(exception); }
        }
    }
    private bool RollbackOrRetain(CharacterActor actor, AgeConditionAnatomyProjectionSaveData projection,
        in CharacterPreparedAgeConditionAnatomyReceipt mutation, bool appliedNow, string failure, out string failureCode)
    {
        if (appliedNow && mutation.Before.IsValid)
        {
            bodyMutations.RestorePreparedAgeConditionAnatomy(actor, mutation.Before, "age-condition-anatomy-observation-rollback");
            store.ResetAgeConditionAnatomyProjection(projection.operationId);
        }
        return Fail(projection, failure, out failureCode);
    }
    private bool Fail(AgeConditionAnatomyProjectionSaveData projection, string failure, out string failureCode)
    { failureCode = failure; store.RecordAgeConditionAnatomyFailure(projection.operationId, failure); return false; }

    private static CharacterPreparedAgeConditionAnatomyReceipt
        RestoreMutationReceipt(AgeConditionAnatomyProjectionSaveData projection)
    {
        CharacterAnatomyDamageDelta[] deltas = projection.nodeDeltas.Select(value =>
            new CharacterAnatomyDamageDelta(
                value.nodeId,
                value.maximumHealth,
                value.healthBefore,
                value.requestedDamage,
                value.healthAfter,
                value.bleedingBefore,
                value.bleedingAfter)).ToArray();
        return new CharacterPreparedAgeConditionAnatomyReceipt(
            new CharacterId(projection.characterId),
            deltas,
            projection.aggregateMaximumHealth,
            projection.aggregateHealthBefore,
            projection.aggregateHealthAfter,
            projection.aggregateWasDowned,
            "age-condition:" + projection.conditionId + ":"
                + ((AgeConditionSeverity)projection.currentSeverity));
    }

    private static float DamageFraction(AgeConditionSeverity severity) =>
        severity switch
        {
            AgeConditionSeverity.Mild => 0.05f,
            AgeConditionSeverity.Moderate => 0.10f,
            AgeConditionSeverity.Severe => 0.20f,
            AgeConditionSeverity.Critical => 0.30f,
            AgeConditionSeverity.OrganFunctionLoss => 1f,
            _ => throw new ArgumentOutOfRangeException(
                nameof(severity), severity, null)
        };
}
