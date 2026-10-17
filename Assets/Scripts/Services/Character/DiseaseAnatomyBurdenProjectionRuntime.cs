using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

public interface IDiseaseAnatomyBurdenProjectionCommand
{
    bool TryResume(string operationId, out string failureCode);
    void ResumePendingRequired();
}

public sealed class DiseaseAnatomyBurdenProjectionRuntime :
    IDiseaseAnatomyBurdenProjectionCommand,
    IStartable,
    IDungeonSaveRestoreCompletedHook
{
    private readonly IDiseaseAnatomyBurdenProjectionStore store;
    private readonly ICharacterWorldQuery world;
    private readonly IAnatomyProfileCatalog anatomyProfiles;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IDiseaseAnatomyBurdenMutationTransaction bodyMutations;
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly HashSet<string> active = new(StringComparer.Ordinal);

    public DiseaseAnatomyBurdenProjectionRuntime(
        IDiseaseAnatomyBurdenProjectionStore store,
        ICharacterWorldQuery world,
        IAnatomyProfileCatalog anatomyProfiles,
        IAnatomyHealthRuntime anatomy,
        IDiseaseAnatomyBurdenMutationTransaction bodyMutations,
        IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher recovery)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.anatomyProfiles = anatomyProfiles
            ?? throw new ArgumentNullException(nameof(anatomyProfiles));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.bodyMutations = bodyMutations
            ?? throw new ArgumentNullException(nameof(bodyMutations));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
    }

    public void Start()
    {
        foreach (DiseaseAnatomyBurdenProjectionSaveData projection in
                 store.PendingAnatomyBurdenProjections.ToArray())
        {
            try
            {
                if (!TryResume(projection.operationId, out string failure))
                {
                    Debug.LogError(
                        "Disease anatomy burden projection remains pending: "
                        + failure);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    public void OnRestoreCompleted() => Start();

    public void ResumePendingRequired()
    {
        var failures = new List<string>();
        foreach (string operationId in store.PendingAnatomyBurdenProjections
                     .Select(value => value.operationId)
                     .ToArray())
        {
            try
            {
                if (!TryResume(operationId, out string failure))
                    failures.Add(operationId + "=" + failure);
            }
            catch (Exception exception)
            {
                failures.Add(operationId + "=exception:"
                    + exception.GetType().Name + ":" + exception.Message);
            }
        }
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Disease anatomy burden projection failed: "
                + string.Join(" | ", failures));
        }
    }

    public bool TryResume(string operationId, out string failureCode)
    {
        if (!active.Add(operationId))
        { failureCode = "disease-anatomy-burden-operation-busy"; return false; }
        try { return Resume(operationId, out failureCode); }
        finally { active.Remove(operationId); }
    }

    private bool Resume(string operationId, out string failureCode)
    {
        var projection = store.PendingAnatomyBurdenProjections.SingleOrDefault(value =>
            string.Equals(value.operationId, operationId, StringComparison.Ordinal));
        if (projection == null)
        { failureCode = "disease-anatomy-burden-projection-missing"; return false; }

        CharacterActor actor = null;
        CharacterPreparedDiseaseAnatomyBurdenReceipt mutation = default;
        bool appliedNow = false, publicationCrossed = false;
        DomainFactLease lease = default;
        try
        {
            if ((DiseaseAnatomyBurdenProjectionPhase)projection.phase == DiseaseAnatomyBurdenProjectionPhase.Pending)
            {
                actor = world.Characters.SingleOrDefault(candidate => candidate != null
                    && CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
                    && id.Value == projection.characterId);
                if (actor == null)
                    return Fail(projection, "disease-anatomy-burden-actor-unavailable", out failureCode);
                if (actor.IsDead)
                    return Fail(projection, "disease-anatomy-burden-actor-dead", out failureCode);
                string nodeId = ResolveTargetNode(actor, projection);
                if (nodeId.Length == 0)
                    return Fail(projection, "disease-anatomy-burden-node-unavailable", out failureCode);
                mutation = bodyMutations.ApplyPreparedDiseaseAnatomyBurden(actor, nodeId,
                    projection.requestedInfectionBurden, "disease:" + projection.diseaseId);
                appliedNow = true;
                store.RecordAnatomyBurdenApplied(operationId, mutation.NodeId,
                    mutation.InfectionBefore, mutation.InfectionAfter, actor.Identity?.DisplayName ?? actor.name);
                projection = store.PendingAnatomyBurdenProjections.Single(value => value.operationId == operationId);
            }

            var fact = new DiseaseAnatomyBurdenAppliedFact(projection);
            DomainFactDelivery delivered;
            if (projection.publicationKey.Length > 0)
            {
                // The stored observation is authoritative after a process boundary.
                // Even a failed replay must never apply this body burden again.
                publicationCrossed = true;
                var replies = new DomainFactDelivery[1];
                bool accepted = recovery.ReplayBatch(new[]
                    { DomainFactReplay.Create(projection.publicationKey, fact) }, replies, out string failure);
                delivered = replies[0];
                if (!accepted)
                    return Fail(projection, "disease-anatomy-burden-observation-pending:" + failure, out failureCode);
            }
            else
            {
                if (!facts.TryReserve(fact, out lease, out string reserveFailure))
                    return RollbackOrRetain(actor, projection, mutation, appliedNow,
                        "disease-anatomy-burden-observation-reserve:" + reserveFailure, out failureCode);
                string key = lease.PublicationKey;
                store.RecordAnatomyBurdenPublication(operationId, key);
                projection.publicationKey = key;
                publicationCrossed = true;
                delivered = facts.Publish(lease, fact);
                if (!delivered.MayBeCommitted)
                {
                    publicationCrossed = false;
                    return RollbackOrRetain(actor, projection, mutation, appliedNow,
                        "disease-anatomy-burden-observation-rejected:" + delivered.Detail, out failureCode);
                }
            }
            if (!delivered.Completed || delivered.OperationKey != projection.publicationKey
                || !DomainFactValueSyntax.IsText(delivered.PublicationId, 1024, false)
                || delivered.Fingerprint?.Length != 64
                || !delivered.Fingerprint.All(value => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
                return Fail(projection, "disease-anatomy-burden-observation-pending:" + delivered.Detail, out failureCode);

            store.CompleteAnatomyBurdenProjection(operationId);
            failureCode = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            if (!publicationCrossed && appliedNow && mutation.Before.IsValid)
            {
                bodyMutations.RestorePreparedDiseaseAnatomyBurden(actor, mutation.Before,
                    "disease-anatomy-burden-exception-rollback");
                store.ResetAnatomyBurdenProjection(operationId);
            }
            store.RecordAnatomyBurdenFailure(operationId,
                "disease-anatomy-burden-exception:" + exception.GetType().Name);
            throw;
        }
        finally
        {
            try { facts.Cancel(lease); }
            catch (Exception exception) when (exception is not OutOfMemoryException
                && exception is not StackOverflowException && exception is not AccessViolationException)
            { Debug.LogException(exception); }
        }
    }

    private string ResolveTargetNode(
        CharacterActor actor,
        DiseaseAnatomyBurdenProjectionSaveData projection)
    {
        AnatomyFunction target = ((DiseaseTargetSystem)projection.targetSystem)
            switch
            {
                DiseaseTargetSystem.Consciousness => AnatomyFunction.Consciousness,
                DiseaseTargetSystem.Breathing => AnatomyFunction.Breathing,
                DiseaseTargetSystem.Digestion => AnatomyFunction.Digestion,
                DiseaseTargetSystem.Filtration => AnatomyFunction.Filtration,
                _ => AnatomyFunction.Core
            };
        HashSet<string> available = anatomy.GetAnatomySnapshot(actor).Nodes
            .Where(value => value != null && !value.missing)
            .Select(value => value.nodeId)
            .ToHashSet(StringComparer.Ordinal);
        AnatomyProfileDefinition profile = anatomyProfiles.GetForSpecies(
            actor.SpeciesTag);
        return profile.Nodes
            .Where(value => (value.Functions & target) != 0
                && available.Contains(value.NodeId))
            .OrderByDescending(value => value.Vital)
            .ThenByDescending(value => value.CapacityWeight)
            .ThenBy(value => value.NodeId, StringComparer.Ordinal)
            .Select(value => value.NodeId)
            .FirstOrDefault() ?? string.Empty;
    }

    private bool RollbackOrRetain(
        CharacterActor actor,
        DiseaseAnatomyBurdenProjectionSaveData projection,
        in CharacterPreparedDiseaseAnatomyBurdenReceipt mutation,
        bool appliedNow,
        string failure,
        out string failureCode)
    {
        if (appliedNow && mutation.Before.IsValid)
        {
            bodyMutations.RestorePreparedDiseaseAnatomyBurden(
                actor,
                mutation.Before,
                "disease-anatomy-burden-outcome-rollback");
            store.ResetAnatomyBurdenProjection(projection.operationId);
        }
        return Fail(projection, failure, out failureCode);
    }

    private bool Fail(
        DiseaseAnatomyBurdenProjectionSaveData projection,
        string failure,
        out string failureCode)
    {
        failureCode = failure;
        store.RecordAnatomyBurdenFailure(projection.operationId, failure);
        return false;
    }
}
