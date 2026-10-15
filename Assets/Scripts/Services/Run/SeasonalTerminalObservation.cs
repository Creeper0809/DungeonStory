using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

// The physical seasonal owner has already committed. Retry only this frozen fact.
internal static class SeasonalTerminalObservation
{
    internal static bool TryPublish<T>(T fact, SeasonalTerminalPublicationSaveData proof,
        IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery, out string failure)
    {
        failure = string.Empty;
        if (publisher == null || recovery == null)
        { failure = "seasonal-terminal-observer-not-connected"; return false; }
        DomainFactLease lease = default;
        try
        {
            DomainFactDelivery delivery;
            if (proof.key.Length == 0)
            {
                if (!publisher.TryReserve(fact, out lease, out failure)) return false;
                if (!DomainFactValueSyntax.IsText(lease.PublicationKey, 4096, false))
                { failure = "seasonal-terminal-publication-key-missing"; return false; }
                proof.key = lease.PublicationKey;
                delivery = publisher.Publish(lease, fact);
            }
            else
            {
                var results = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(proof.key, fact) }, results, out failure)) return false;
                delivery = results[0];
            }
            if (delivery.Phase is not (DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery)
                || delivery.OperationKey != proof.key || !DomainFactValueSyntax.IsText(delivery.PublicationId, 4096, false)
                || delivery.Fingerprint?.Length != 64
                || delivery.Fingerprint.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            { failure = "seasonal-terminal-observation-unconfirmed:" + delivery.Detail; return false; }
            proof.id = delivery.PublicationId;
            proof.fingerprint = delivery.Fingerprint;
            return true;
        }
        catch (Exception error) when (error is not OutOfMemoryException && error is not StackOverflowException && error is not AccessViolationException)
        { failure = "seasonal-terminal-observation-unconfirmed:" + error.Message; return false; }
        finally { if (lease.IsValid) publisher.Cancel(lease); }
    }

    internal static void ValidateWildlife(V20ActiveEventSaveData occurrence)
    {
        var owner = occurrence.seasonalWildlifeArrival;
        if (owner.observation == null || owner.publication == null)
            throw new InvalidOperationException("seasonal-arrival-observation-missing");
        bool hasFact = !owner.observation.IsEmpty;
        owner.publication.Validate(owner.outcomeResultKey, hasFact);
        if (!hasFact) return;
        var fact = new SeasonalWildlifeArrivalFact(owner.observation).Snapshot();
        if (!owner.configured || owner.members.Count != owner.exactCount || owner.members.Any(value => !value.spawned)
            || fact.occurrenceId != occurrence.instanceId || fact.speciesId != owner.speciesId || fact.habitatId != owner.requiredHabitatId
            || fact.absoluteDay < occurrence.startedAbsoluteDay
            || !fact.wildlifeIds.SequenceEqual(owner.members.Select(member => member.wildlifeId).OrderBy(id => id, StringComparer.Ordinal)))
            throw new InvalidOperationException("seasonal-arrival-observation-owner-conflict");
    }

    internal static void ValidateCargo(V20ActiveEventSaveData occurrence)
    {
        var owner = occurrence.seasonalDriftCargo;
        if (owner.observation == null || owner.publication == null)
            throw new InvalidOperationException("seasonal-cargo-observation-missing");
        bool hasFact = !owner.observation.IsEmpty;
        owner.publication.Validate(owner.outcomeResultKey, hasFact);
        if (!hasFact) return;
        var fact = new SeasonalDriftCargoFact(owner.observation).Snapshot();
        if (!owner.configured || !owner.spawned || !owner.expirationCompleted || fact.occurrenceId != occurrence.instanceId
            || fact.itemId != owner.itemId || fact.absoluteDay < occurrence.startedAbsoluteDay
            || fact.exactQuantity != owner.exactQuantity || fact.securedQuantity != owner.securedQuantity
            || fact.expiredQuantity != owner.expiredQuantity || fact.expiredMassGrams != owner.expiredMassGrams
            || fact.dispositionCommitId != owner.dispositionCommitId || fact.positionX != owner.positionX || fact.positionY != owner.positionY
            || !fact.sourceStackIds.SequenceEqual(owner.sourceStackIds.OrderBy(id => id, StringComparer.Ordinal)))
            throw new InvalidOperationException("seasonal-cargo-observation-owner-conflict");
    }
}
